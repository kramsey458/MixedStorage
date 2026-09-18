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
