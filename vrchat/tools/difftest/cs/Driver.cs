using System;using System.IO;using System.Linq;using System.Reflection;using System.Text;using System.Text.Json;using System.Runtime.CompilerServices;
class Driver{
  static object g; static Type T=typeof(KeyswitchCrusher); const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static object Call(string m,params object[] a){ return T.GetMethod(m,F).Invoke(g,a); }
  static object Get(string f){ return T.GetField(f,F).GetValue(g); }
  static void Set(string f,object v){ T.GetField(f,F).SetValue(g,v); }
  static string D(double d){ return d.ToString("R",System.Globalization.CultureInfo.InvariantCulture); }
  static void Main(string[] a){
    g=RuntimeHelpers.GetUninitializedObject(T);
    Call("Init"); Set("ready",true);
    var ops=JsonDocument.Parse(File.ReadAllText(a[0])).RootElement;
    var sb=new StringBuilder(); int nAch=((bool[])Get("ach")).Length;
    foreach(var op in ops.EnumerateArray()){
      switch(op[0].GetString()){
        case "hit": Call("HitCore",op[1].GetBoolean()); break;
        case "key": Set("keys",(int)Get("keys")+1); break;
        case "tick": Call("Tick",Math.Min(op[1].GetDouble(),3600.0)); break; // Update と同じ上限
        case "mode": Set("buyMode",op[1].GetInt32()); break;
        case "buy": Call("BuyItem",op[1].GetInt32()); break;
        case "up": Call("BuyUp",op[1].GetInt32()); break;
        case "gold": Call("TakeGoldCore",op[1].GetDouble()); break;
        case "ps": Call("DoPrestige"); break;
      }
      Call("CheckAch",0,nAch);
      sb.Append("{");
      foreach(var k in new[]{"scrap","total","runTotal","hp","maxHit","playSec"}) sb.Append($"\"{k}\":{D((double)Get(k))},");
      foreach(var k in new[]{"broken","level","clicks","crits","keys","maxCombo","bp","prestiges","kbAll","brokenAll","gold"}) sb.Append($"\"{k}\":{Get(k)},");
      sb.Append($"\"fast\":{((bool)Get("fast")?1:0)},");
      sb.Append($"\"owned\":\"{string.Join(",",(int[])Get("owned"))}\",");
      sb.Append($"\"ups\":\"{string.Concat(((bool[])Get("ups")).Select(b=>b?"1":"0"))}\",");
      sb.Append($"\"ach\":\"{string.Concat(((bool[])Get("ach")).Select(b=>b?"1":"0"))}\",");
      sb.Append($"\"click\":{D((double)Call("ClickPower"))},\"auto\":{D((double)Call("AutoPower"))},");
      sb.Append($"\"fmt\":\"{Call("Fmt",Get("scrap"))}|{Call("Fmt",Get("total"))}|{Call("Fmt",Get("hp"))}\"}}\n");
    }
    File.WriteAllText(a[1],sb.ToString());
    // 保存して、まっさらな別の 1 台で読み込み、同じ状態に戻るかを見る
    string[] nums={"scrap","total","runTotal","hp","maxHit","playSec","broken","level","clicks","crits","keys","maxCombo","bp","prestiges","kbAll","brokenAll","gold","fast","sw","buyMode","soundOn","achOwned","upCount"};
    Func<string> state=()=>string.Join("|",nums.Select(k=>Get(k).ToString()))+"|"+string.Join(",",(int[])Get("owned"))+"|"+string.Concat(((bool[])Get("ups")).Select(b=>b?"1":"0"))+"|"+string.Concat(((bool[])Get("ach")).Select(b=>b?"1":"0"))+"|"+string.Join(",",(int[])Get("vol"))+"|"+D((double)Call("ClickPower"))+"|"+D((double)Call("AutoPower"));
    Set("feverOn",false); Set("powerDirty",true); // フィーバーは保存しない(ブラウザ版も同じ)
    Set("sw",3); Set("buyMode",2); Set("soundOn",false); ((int[])Get("vol"))[2]=40;
    Set("restored",true); Set("ownedDirty",true); Set("upsDirty",true); Set("achDirty",true);
    Call("Save"); string before=state();
    g=RuntimeHelpers.GetUninitializedObject(T); Call("Init"); Call("Load",new object[]{null}); string after=state();
    Console.WriteLine(before==after?"save/load round trip OK":"SAVE/LOAD MISMATCH\n"+before+"\n"+after);
    Console.WriteLine("cs done level "+Get("level")+" prestiges "+Get("prestiges"));
  }
}
