using MixedStorage;
using UnityEngine;
int count = 0;
void Check(bool ok, string name) { count++; if (!ok) throw new Exception(name); }
GameApiTests.Run(args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Timberborn",
 args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "source", "bin", "Release", "netstandard2.1", "MixedStorage.dll"));

var cellPoints = new[] { new Vector3(-1,0,0), new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
var cellPattern = new[] {0,1,2,0,2,3};
var wholePoints = Enumerable.Range(0,10).SelectMany(i => cellPoints.Select(p => p + new Vector3(i*2,0,0))).ToArray();
var wholeIndices = Enumerable.Range(0,10).SelectMany(i => cellPattern.Select(n => n+i*4)).ToArray();
var identity = Matrix4x4.identity;
var patterns = new[] { cellPattern };
var sizes = new[] { 4 };
// UV2 = (cell, corner) and UV = source vertex number, so every output vertex can be traced to its source.
var whole = new MeshData();
whole.Positions.AddRange(wholePoints);
whole.Normals.AddRange(Enumerable.Repeat(Vector3.up,40));
whole.UV2.AddRange(Enumerable.Range(0,40).Select(v => new Vector2(v / 4, v % 4)));
whole.UV.AddRange(Enumerable.Range(0,40).Select(v => new Vector2(v, -v)));
whole.Indices.AddRange(wholeIndices);
for(int split=0;split<=200;split++)
{
 float boundary=-1+split*.1f;
 var first=new MeshData(); var second=new MeshData();
 Check(WholeCellGeometry.Extract(whole,identity,identity,patterns,sizes,-1,boundary,false,first),"Known topology is recognized");
 Check(WholeCellGeometry.Extract(whole,identity,identity,patterns,sizes,boundary,19,true,second),"Known topology is recognized");
 Check(first.Indices.Count+second.Indices.Count==wholeIndices.Length,"All whole cells assigned exactly once");
 Check(!first.UV2.Select(n=>(int)n.x).Intersect(second.UV2.Select(n=>(int)n.x)).Any(),"Boundary cell has only one owner");
 foreach(var section in new[]{first,second})
 {
  Check(section.Positions.Count==section.Normals.Count && section.Positions.Count==section.UV.Count && section.Positions.Count==section.UV2.Count,"Vertex streams stay aligned");
  Check(section.Indices.Count==0 || section.Indices.Max()+1==section.Positions.Count,"Only the vertices of selected cells are kept");
  foreach(var group in section.Indices.GroupBy(i=>i/4))
   Check(group.SequenceEqual(cellPattern.Select(i=>i+group.Key*4)),"Complete cell topology retained across arbitrary boundary");
  for(int v=0;v<section.Positions.Count;v++)
  {
   int source=(int)section.UV2[v].x*4+(int)section.UV2[v].y;
   Check(section.Positions[v]==wholePoints[source] && section.UV[v]==whole.UV[source],"Each vertex keeps its own attributes");
  }
 }
}
var broken=new MeshData(); broken.Positions.AddRange(wholePoints); broken.Indices.AddRange(new[]{0,2,1});
Check(!WholeCellGeometry.Extract(broken,identity,identity,patterns,sizes,-1,19,true,new MeshData()),"Unrecognized topology requires whole-mesh fallback");
var extra=new MeshData(); extra.Positions.AddRange(wholePoints.Append(Vector3.zero)); extra.Indices.AddRange(wholeIndices);
Check(!WholeCellGeometry.Extract(extra,identity,identity,patterns,sizes,-1,19,true,new MeshData()),"Vertices outside every cell require whole-mesh fallback");
Console.WriteLine("PASS: whole-cell partitions across 201 boundaries: exact ownership, complete topology, compact vertices, per-vertex attributes, unknown-topology fallback.");

// Reference: the original algorithm. It moved every vertex of the mesh into section space for every
// section and kept the full vertex array with only the selected triangles.
Matrix4x4 Transform(double yaw, double roll, float scale, Vector3 offset)
{
 double cy=Math.Cos(yaw), sy=Math.Sin(yaw), cr=Math.Cos(roll), sr=Math.Sin(roll);
 // Rotation about z (roll) after rotation about y (yaw), scaled uniformly.
 double[,] r={{cr*cy,-sr,cr*sy},{sr*cy,cr,sr*sy},{-sy,0,cy}};
 var m=Matrix4x4.identity;
 m.m00=scale*(float)r[0,0]; m.m01=scale*(float)r[0,1]; m.m02=scale*(float)r[0,2]; m.m03=offset.x;
 m.m10=scale*(float)r[1,0]; m.m11=scale*(float)r[1,1]; m.m12=scale*(float)r[1,2]; m.m13=offset.y;
 m.m20=scale*(float)r[2,0]; m.m21=scale*(float)r[2,1]; m.m22=scale*(float)r[2,2]; m.m23=offset.z;
 return m;
}
// Inverse transpose of a rotation with uniform scale is the rotation itself; the normals are renormalized anyway.
Matrix4x4 RotationOnly(Matrix4x4 m,float scale)
{
 var n=Matrix4x4.identity;
 n.m00=m.m00/scale; n.m01=m.m01/scale; n.m02=m.m02/scale;
 n.m10=m.m10/scale; n.m11=m.m11/scale; n.m12=m.m12/scale;
 n.m20=m.m20/scale; n.m21=m.m21/scale; n.m22=m.m22/scale;
 return n;
}
List<int[]> ReferenceTriangles(MeshData source,MeshData moved,Matrix4x4 transform,Matrix4x4 normalMatrix,int[][] cellPatterns,int[] cellSizes,float min,float max,bool last)
{
 moved.Clear();
 foreach(var p in source.Positions) moved.Positions.Add(transform.MultiplyPoint3x4(p));
 foreach(var n in source.Normals) moved.Normals.Add(normalMatrix.MultiplyVector(n).normalized);
 foreach(var t in source.Tangents) { var d=transform.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized; moved.Tangents.Add(new Vector4(d.x,d.y,d.z,t.w)); }
 var triangles=new List<int[]>();
 int vertex=0, triangle=0;
 while(triangle<source.Indices.Count)
 {
  int match=-1;
  for(int p=0;p<cellPatterns.Length && match<0;p++)
  {
   var pattern=cellPatterns[p];
   if(triangle+pattern.Length>source.Indices.Count || vertex+cellSizes[p]>source.Positions.Count) continue;
   if(Enumerable.Range(0,pattern.Length).All(i=>source.Indices[triangle+i]==vertex+pattern[i])) match=p;
  }
  Check(match>=0,"Reference mesh is decomposable");
  var xs=Enumerable.Range(vertex,cellSizes[match]).Select(i=>moved.Positions[i].x).ToArray();
  float center=(xs.Min()+xs.Max())*.5f;
  if(center>=min && (center<max || last && center<=max))
   for(int i=0;i<cellPatterns[match].Length;i+=3)
    triangles.Add(new[]{cellPatterns[match][i],cellPatterns[match][i+1],cellPatterns[match][i+2]}.Select(k=>k+vertex).ToArray());
  triangle+=cellPatterns[match].Length; vertex+=cellSizes[match];
 }
 return triangles;
}
// A triangle is identified by the source vertex numbers stored in UV.x, whatever its vertex indices are.
string Key(int[] ids,MeshData data) => string.Join(",",ids.Select(i=>(int)data.UV[i].x));
bool Near(Vector3 x,Vector3 y)=>(x-y).magnitude<1e-3f;
var quad=new[]{0,1,2,0,2,3};
var box=new[]{4,6,5,4,7,6, 0,1,2,0,2,3, 0,4,5,0,5,1, 1,5,6,1,6,2, 2,6,7,2,7,3, 3,7,4,3,4,0};
var cellKinds=new[]{quad,box}; var cellSizes=new[]{4,8};
int compared=0;
var gen=new System.Random(4);
var moved=new MeshData();
for(int run=0;run<400;run++)
{
 // A random pile: cells of both kinds at random positions, each vertex tagged with its own number.
 var mesh=new MeshData(); int cells=gen.Next(1,60), vertexBase=0;
 for(int cell=0;cell<cells;cell++)
 {
  int kind=gen.Next(2), vertices=cellSizes[kind];
  float cx=(float)(gen.NextDouble()*40-20), cy=(float)(gen.NextDouble()*6), cz=(float)(gen.NextDouble()*6);
  for(int v=0;v<vertices;v++)
  {
   mesh.Positions.Add(new Vector3(cx+(v%2==0?-.4f:.4f),cy+(v/2%2==0?0:.8f),cz+(v/4%2==0?-.4f:.4f)));
   var normal=new Vector3((float)gen.NextDouble()-.5f,(float)gen.NextDouble()-.5f,(float)gen.NextDouble()-.5f).normalized;
   mesh.Normals.Add(normal); mesh.Tangents.Add(new Vector4(normal.y,-normal.x,0,1));
   mesh.UV.Add(new Vector2(vertexBase+v,cell)); mesh.UV2.Add(new Vector2(cell,v)); mesh.Colors.Add(new Color32((byte)cell,(byte)v,(byte)kind,255));
  }
  mesh.Indices.AddRange(cellKinds[kind].Select(i=>i+vertexBase)); vertexBase+=vertices;
 }
 bool plain=run%2==0;
 float scale=plain?1:(float)(.5+gen.NextDouble());
 var offset=new Vector3((float)(gen.NextDouble()*20-10),(float)gen.NextDouble()*4,(float)gen.NextDouble()*20);
 var transform=plain?Transform(0,0,1,offset):Transform(gen.NextDouble()*6.28,gen.NextDouble()*.6-.3,scale,offset);
 Check(WholeCellGeometry.IsTranslation(transform)==plain,"Translation-only transforms are recognized");
 var normalMatrix=plain?identity:RotationOnly(transform,scale);
 ReferenceTriangles(mesh,moved,transform,normalMatrix,cellKinds,cellSizes,0,0,false);
 float low=moved.Positions.Min(p=>p.x)-1, high=moved.Positions.Max(p=>p.x)+1;
 var cuts=Enumerable.Range(0,gen.Next(0,5)).Select(_=>low+(float)gen.NextDouble()*(high-low)).Append(low).Append(high).Order().ToArray();
 var seen=new HashSet<string>();
 for(int s=0;s<cuts.Length-1;s++)
 {
  bool isLast=s==cuts.Length-2;
  var expected=ReferenceTriangles(mesh,moved,transform,normalMatrix,cellKinds,cellSizes,cuts[s],cuts[s+1],isLast);
  var actual=new MeshData();
  Check(WholeCellGeometry.Extract(mesh,transform,normalMatrix,cellKinds,cellSizes,cuts[s],cuts[s+1],isLast,actual),"Random pile is recognized");
  Check(actual.Indices.Count==expected.Count*3,"Same triangles selected as the reference");
  // The reference keeps the source vertex numbering, so its UVs are the source's.
  var byKey=expected.ToDictionary(e=>Key(e,mesh));
  for(int t=0;t<actual.Indices.Count;t+=3)
  {
   var ids=new[]{actual.Indices[t],actual.Indices[t+1],actual.Indices[t+2]};
   Check(byKey.TryGetValue(Key(ids,actual),out var match),"Selected triangle exists in the reference");
   Check(seen.Add(Key(ids,actual)),"No triangle is drawn by two sections");
   for(int k=0;k<3;k++)
   {
    int drawn=ids[k], reference=match[k], source=(int)actual.UV[drawn].x;
    Check(Near(actual.Positions[drawn],moved.Positions[reference]) && Near(actual.Normals[drawn],moved.Normals[reference]) &&
      Near(new Vector3(actual.Tangents[drawn].x,actual.Tangents[drawn].y,actual.Tangents[drawn].z),new Vector3(moved.Tangents[reference].x,moved.Tangents[reference].y,moved.Tangents[reference].z)) &&
      actual.Tangents[drawn].w==moved.Tangents[reference].w,"Transformed vertex data matches the reference");
    Check(actual.UV[drawn]==mesh.UV[source] && actual.UV2[drawn]==mesh.UV2[source] && actual.Colors[drawn].Equals(mesh.Colors[source]),"Untransformed streams are copied verbatim");
    compared++;
   }
  }
 }
 Check(seen.Count==mesh.Indices.Count/3,"Every triangle of the pile is drawn by exactly one section");
}
Console.WriteLine($"PASS: {compared} vertices of random piles (plain and rotated/scaled transforms) match the reference algorithm.");

// Continuous bulk surfaces are fitted whole into their section.
var bulk=new MeshData();
bulk.Positions.AddRange(new[]{new Vector3(-10,0,0),new Vector3(10,0,0),new Vector3(10,2,1),new Vector3(-10,2,1)});
bulk.Normals.AddRange(new[]{new Vector3(1,1,0).normalized,Vector3.up,Vector3.up,Vector3.up});
bulk.Indices.AddRange(new[]{0,1,2,0,2,3});
var narrow=WholeCellGeometry.FitWhole(bulk,identity,0,5);
// A squeeze along x has the inverse transpose diag(1/scale, 1, 1).
var squeezeNormals=Matrix4x4.identity; squeezeNormals.m00=1/narrow.m00;
var fitted=new MeshData(); WholeCellGeometry.TransformAll(bulk,narrow,squeezeNormals,fitted);
Check(Math.Abs(fitted.Positions.Min(p=>p.x)-0)<1e-4 && Math.Abs(fitted.Positions.Max(p=>p.x)-5)<1e-4,"Wide bulk mesh is squeezed into its section");
Check(Near(fitted.Normals[0],new Vector3(4,1,0).normalized) && Near(fitted.Normals[1],Vector3.up),"Fitting transforms normals with the supplied normal matrix");
Check(fitted.Positions.Select(p=>p.y).SequenceEqual(bulk.Positions.Select(p=>p.y)) && fitted.Indices.SequenceEqual(bulk.Indices),"Fitting keeps height and topology");
var wide=WholeCellGeometry.FitWhole(bulk,identity,100,140);
var centered=new MeshData(); WholeCellGeometry.TransformAll(bulk,wide,identity,centered);
Check(Math.Abs(centered.Positions.Min(p=>p.x)-110)<1e-4 && Math.Abs(centered.Positions.Max(p=>p.x)-130)<1e-4,"Narrow bulk mesh keeps its size and is centered in a wider section");
Console.WriteLine("PASS: whole-mesh fallback fits bulk surfaces into their sections.");
