// UdonSharp のスクリプトが呼ぶ外部の命令を拾い、Udon が公開している一覧と突き合わせる
using System;using System.Collections.Generic;using System.IO;using System.Linq;
using System.Reflection.Metadata;using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;using Microsoft.CodeAnalysis.CSharp.Syntax;
class P{
  static HashSet<string> exposed;
  static string TN(ITypeSymbol t){
    if(t is IArrayTypeSymbol a) return TN(a.ElementType)+"Array";
    if(t is ITypeParameterSymbol) return "T";
    string s=t.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle:SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,genericsOptions:SymbolDisplayGenericsOptions.None));
    var n=t as INamedTypeSymbol; string g=""; if(n!=null&&n.IsGenericType) g=string.Concat(n.TypeArguments.Select(TN));
    return s.Replace(".","").Replace("+","")+g;
  }
  static string Sig(IMethodSymbol m){
    string name=m.MethodKind==MethodKind.Constructor?"ctor":m.Name;
    string ps=string.Join("_",m.Parameters.Select(p=>TN(p.Type)+(p.RefKind!=RefKind.None?"Ref":"")));
    string ret=m.MethodKind==MethodKind.Constructor?TN(m.ContainingType):TN(m.ReturnType);
    return "__"+name+(ps.Length>0?"__"+ps:"")+"__"+ret;
  }
  static IEnumerable<ITypeSymbol> Chain(ITypeSymbol t){ for(var x=t;x!=null;x=x.BaseType) yield return x; }
  static int bad=0,ok=0; static HashSet<string> seen=new HashSet<string>();
  static void Check(ITypeSymbol recv,IMethodSymbol m,SyntaxNode at){
    if(m==null) return; var ct=m.ContainingType; if(ct==null) return;
    if(ct.Locations.Any(l=>l.IsInSource)&&!ct.ContainingNamespace.ToDisplayString().StartsWith("VRC")&&ct.Name!="UdonSharpBehaviour") return; // 自分のクラス
    if(ct.Name=="UdonSharpBehaviour") return;
    if(ct.SpecialType==SpecialType.System_String&&m.MethodKind==MethodKind.BuiltinOperator) return;
    var sig=Sig(m); var cands=new List<ITypeSymbol>(); if(recv!=null) cands.AddRange(Chain(recv)); cands.AddRange(Chain(ct));
    foreach(var c in cands){ if(exposed.Contains("Extern"+TN(c)+"."+sig)){ ok++; return; } }
    string key=TN(recv??ct)+"."+sig; if(seen.Add(key)){ bad++; Console.WriteLine("NOT EXPOSED: "+key+"  @"+at.GetLocation().GetLineSpan().StartLinePosition.Line); }
  }
  static void CheckType(ITypeSymbol t,SyntaxNode at){
    if(t==null||t.TypeKind==TypeKind.Error) return;
    if(t.Locations.Any(l=>l.IsInSource)&&t.BaseType!=null&&Chain(t).Any(x=>x.Name=="UdonSharpBehaviour")) return;
    string n=TN(t); if(n=="void") return;
    if(exposed.Any(e=>e.StartsWith("Extern"+n+"."))) return; // ラッパーがある型
    if(typeNames.Contains(n)) return;
    if(seen.Add("T:"+n)){ bad++; Console.WriteLine("TYPE NOT FOUND: "+n+"  @"+at.GetLocation().GetLineSpan().StartLinePosition.Line); }
  }
  static HashSet<string> typeNames=new HashSet<string>();
  // dump <dll...>: ラッパーの DLL から「型.命令」の一覧を書き出す
  static void Dump(IEnumerable<string> dlls){
    foreach(var path in dlls){ using var fs=File.OpenRead(path); using var pe=new PEReader(fs); var md=pe.GetMetadataReader();
      foreach(var th in md.TypeDefinitions){ var t=md.GetTypeDefinition(th); var tn=md.GetString(t.Name);
        foreach(var mh in t.GetMethods()) Console.WriteLine(tn+"."+md.GetString(md.GetMethodDefinition(mh).Name)); } }
  }
  static int Main(string[] a){
    if(a[0]=="dump"){ Dump(a.Skip(1)); return 0; }
    var lines=File.ReadAllLines(a[0]); exposed=new HashSet<string>(lines);
    foreach(var l in lines){ int i=l.IndexOf(".__"); if(i<0) continue; foreach(var part in l.Substring(i+1).Split(new[]{"__"},StringSplitOptions.RemoveEmptyEntries)) foreach(var tn in part.Split('_')) typeNames.Add(tn.EndsWith("Ref")?tn.Substring(0,tn.Length-3):tn); }
    var srcs=File.ReadAllLines(a[1]).Where(File.Exists).ToList(); var refs=File.ReadAllLines(a[2]).Where(File.Exists).ToList();
    var po=new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:new[]{"UNITY_2022_3_OR_NEWER","UDON","VRC_SDK_VRCSDK3"});
    var trees=srcs.Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(f),po,f)).ToList();
    var comp=CSharpCompilation.Create("x",trees,refs.Select(r=>MetadataReference.CreateFromFile(r)),new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    foreach(var tree in trees.Where(t=>t.FilePath.Contains("KeyswitchCrusher/Scripts"))){
      var sm=comp.GetSemanticModel(tree);
      foreach(var d in sm.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error)) Console.WriteLine("COMPILE ERROR: "+d);
      foreach(var n in tree.GetRoot().DescendantNodes()){
        switch(n){
          case InvocationExpressionSyntax inv:{ var m=sm.GetSymbolInfo(inv).Symbol as IMethodSymbol; ITypeSymbol recv=null; if(inv.Expression is MemberAccessExpressionSyntax ma&&m!=null&&!m.IsStatic) recv=sm.GetTypeInfo(ma.Expression).Type; Check(recv,m,n); break; }
          case ObjectCreationExpressionSyntax oc:{ Check(null,sm.GetSymbolInfo(oc).Symbol as IMethodSymbol,n); break; }
          case ArrayCreationExpressionSyntax ac:{ CheckType(sm.GetTypeInfo(ac).Type,n); break; }
          case BinaryExpressionSyntax be:{ var m=sm.GetSymbolInfo(be).Symbol as IMethodSymbol; if(m!=null&&m.MethodKind==MethodKind.UserDefinedOperator) Check(null,m,n); break; }
          case PrefixUnaryExpressionSyntax pu:{ var m=sm.GetSymbolInfo(pu).Symbol as IMethodSymbol; if(m!=null&&m.MethodKind==MethodKind.UserDefinedOperator) Check(null,m,n); break; }
          case VariableDeclarationSyntax vd:{ CheckType(sm.GetTypeInfo(vd.Type).Type,n); break; }
          case IdentifierNameSyntax id:{
            var s=sm.GetSymbolInfo(id).Symbol; if(s==null) break;
            ITypeSymbol recv=null; SyntaxNode whole=id;
            if(id.Parent is MemberAccessExpressionSyntax pma&&pma.Name==id){ recv=sm.GetTypeInfo(pma.Expression).Type; whole=pma; }
            bool write=whole.Parent is AssignmentExpressionSyntax asg&&asg.Left==whole; bool compound=write&&!((AssignmentExpressionSyntax)whole.Parent).IsKind(SyntaxKind.SimpleAssignmentExpression);
            if(s is IPropertySymbol p){ if(!write||compound) Check(p.IsStatic?null:recv,p.GetMethod,n); if(write) Check(p.IsStatic?null:recv,p.SetMethod,n); }
            else if(s is IFieldSymbol f&&!f.IsConst&&!f.ContainingType.Locations.Any(l=>l.IsInSource)&&f.ContainingType.TypeKind!=TypeKind.Enum){
              string tn=TN(f.ContainingType); string g="Extern"+tn+".__get_"+f.Name+"__"+TN(f.Type), st="Extern"+tn+".__set_"+f.Name+"__"+TN(f.Type);
              if((!write||compound)&&!exposed.Contains(g)&&seen.Add(g)){bad++;Console.WriteLine("NOT EXPOSED: "+g);} else ok++;
              if(write&&!exposed.Contains(st)&&seen.Add(st)){bad++;Console.WriteLine("NOT EXPOSED: "+st);}
            }
            break; }
        }
      }
    }
    Console.WriteLine($"checked ok={ok} notExposed={bad}");
    return bad==0?0:1;
  }
}
