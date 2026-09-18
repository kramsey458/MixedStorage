using MixedStorage;
using UnityEngine;
int count = 0;
void Check(bool ok, string name) { count++; if (!ok) throw new Exception(name); }
double Area(Vector3[] p) { double a=0; for(int i=0;i<p.Length;i++) {var b=p[(i+1)%p.Length]; a += p[i].x*b.y-b.x*p[i].y;} return Math.Abs(a)/2; }
var a=new Vector3(-1,0,0); var b=new Vector3(1,0,0); var c=new Vector3(0,2,0);
Check(StorageMeshClipper.ClipTriangle(a,b,c,2,3).Length==0,"Outside is empty");
Check(Math.Abs(Area(StorageMeshClipper.ClipTriangle(a,b,c,-2,2))-2)<1e-6,"Inside area conserved");
var left=StorageMeshClipper.ClipTriangle(a,b,c,-1,0);
var right=StorageMeshClipper.ClipTriangle(a,b,c,0,1);
Check(Math.Abs(Area(left)-1)<1e-6 && Math.Abs(Area(right)-1)<1e-6,"50/50 split");
Check(left.All(v=>v.x<=0)&&right.All(v=>v.x>=0),"No crossing boundaries");
Check(Area(StorageMeshClipper.ClipTriangle(a,b,c,0,0))==0,"Zero-width has zero area");
var random=new System.Random(57);
for(int run=0;run<1000;run++) {
 var cuts=Enumerable.Range(0,random.Next(1,28)).Select(_=>(float)(random.NextDouble()*2-1)).Append(-1).Append(1).Order().ToArray();
 double sum=0;
 for(int i=0;i<cuts.Length-1;i++) {
  var p=StorageMeshClipper.ClipTriangle(a,b,c,cuts[i],cuts[i+1]); sum+=Area(p);
  Check(p.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&v.x>=cuts[i]-1e-5&&v.x<=cuts[i+1]+1e-5),"Finite vertices within section");
 }
 Check(Math.Abs(sum-2)<1e-5,"Random partitions conserve total area");
}
Console.WriteLine($"PASS: {count} mesh clipping assertions.");
GameApiTests.Run(args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Timberborn");
var cellPoints = new[] { new Vector3(-1,0,0), new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
var cellPattern = new[] {0,1,2,0,2,3};
var wholePoints = Enumerable.Range(0,10).SelectMany(i => cellPoints.Select(p => p + new Vector3(i*2,0,0))).ToArray();
var wholeIndices = Enumerable.Range(0,10).SelectMany(i => cellPattern.Select(n => n+i*4)).ToArray();
for(int split=0;split<=200;split++)
{
 float boundary=-1+split*.1f;
 var first=WholeCellGeometry.Select(wholePoints,wholeIndices,new[]{cellPattern},new[]{4},-1,boundary,false);
 var second=WholeCellGeometry.Select(wholePoints,wholeIndices,new[]{cellPattern},new[]{4},boundary,19,true);
 Check(first.Length+second.Length==wholeIndices.Length,"All whole cells assigned exactly once");
 Check(!first.Intersect(second).Any(),"Boundary cell has only one owner");
 foreach(var section in new[]{first,second})
  foreach(var group in section.GroupBy(i=>i/4))
   Check(group.SequenceEqual(cellPattern.Select(i=>i+group.Key*4)),"Complete cell topology retained across arbitrary boundary");
}
Check(WholeCellGeometry.Select(wholePoints,new[]{0,2,1},new[]{cellPattern},new[]{4},-1,19,true)==null,"Unrecognized topology requires whole-mesh fallback");
Console.WriteLine("PASS: 201 whole-cell partitions, exact boundary ownership, complete topology, and unknown-topology fallback.");
