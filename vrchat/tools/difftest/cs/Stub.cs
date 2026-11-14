// 試験用: 計算の部分だけを動かすための、Unity・VRChat・UdonSharp の最小の代用品。
// 型が本物と合っているかは、別に本物の SDK を相手にしたコンパイルで確かめている
using System;
namespace UnityEngine {
  public class Object { public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b); public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b); public override bool Equals(object o) => ReferenceEquals(this, o); public override int GetHashCode() => 0; }
  public class Component : Object { public Transform transform; public GameObject gameObject; }
  public class Behaviour : Component {}
  public class MonoBehaviour : Behaviour {}
  public class HeaderAttribute : Attribute { public HeaderAttribute(string s) {} }
  public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    public static Vector3 one => new Vector3(1, 1, 1);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
    public static float Distance(Vector3 a, Vector3 b) => 0; }
  public struct Color { public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; } public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    public static Color white => new Color(1, 1, 1, 1); public static Color HSVToRGB(float h, float s, float v) => white; public static Color operator *(Color c, float f) => c; }
  public class Transform : Component { public Vector3 localPosition, position, localScale; public void Rotate(float x, float y, float z) {} }
  public class GameObject : Object { public Transform transform; public void SetActive(bool b) {} }
  public class Material : Object { public Color color; public void SetColor(string n, Color c) {} }
  public class Renderer : Component { public Material sharedMaterial; }
  public class ParticleSystem : Component { public void Emit(int n) {} }
  public class AudioClip : Object {}
  public class AudioSource : Behaviour { public float pitch; public void PlayOneShot(AudioClip c, float v) {} }
  public static class Time { public static float deltaTime; }
  public static class Random { public static float value => 0.5f; }
  public enum KeyCode { A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9 }
  public static class Input { public static bool GetKeyDown(KeyCode k) => false; }
  public enum HumanBodyBones { LeftHand, RightHand }
  public static class Mathf {
    public static int Min(int a, int b) => Math.Min(a, b); public static int Max(int a, int b) => Math.Max(a, b);
    public static int Clamp(int v, int a, int b) => Math.Max(a, Math.Min(b, v)); public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
    public static float Clamp01(float v) => Clamp(v, 0, 1); public static float Log10(float f) => (float)Math.Log10(f);
    public static float Sin(float f) => (float)Math.Sin(f); public static float Cos(float f) => (float)Math.Cos(f); public static float Repeat(float t, float l) => t % l; }
}
namespace UnityEngine.UI { public class Text : UnityEngine.Behaviour { public string text; public UnityEngine.Color color; } }
namespace VRC.SDKBase {
  public class VRCPlayerApi { public bool isLocal; public bool IsUserInVR() => false; public UnityEngine.Vector3 GetBonePosition(UnityEngine.HumanBodyBones b) => default; public UnityEngine.Vector3 GetPosition() => default;
    public void PlayHapticEventInHand(VRC_Pickup.PickupHand h, float d, float a, float f) {} }
  public static class Networking { public static VRCPlayerApi LocalPlayer => null; }
  public class VRC_Pickup { public enum PickupHand { None, Left, Right } }
}
namespace VRC.SDK3.Persistence {
  using VRC.SDKBase;
  // 保存先は、試験の間だけ覚えておく表
  public static class PlayerData {
    public static readonly System.Collections.Generic.Dictionary<string, object> Store = new System.Collections.Generic.Dictionary<string, object>();
    public static bool HasKey(VRCPlayerApi p, string k) => Store.ContainsKey(k);
    public static double GetDouble(VRCPlayerApi p, string k) => (double)Store[k]; public static int GetInt(VRCPlayerApi p, string k) => (int)Store[k];
    public static bool GetBool(VRCPlayerApi p, string k) => (bool)Store[k]; public static byte[] GetBytes(VRCPlayerApi p, string k) => (byte[])Store[k];
    public static void SetDouble(string k, double v) { Store[k] = v; } public static void SetInt(string k, int v) { Store[k] = v; }
    public static void SetBool(string k, bool v) { Store[k] = v; } public static void SetBytes(string k, byte[] v) { Store[k] = (byte[])v.Clone(); } }
}
namespace UdonSharp {
  public enum BehaviourSyncMode { None }
  public class UdonBehaviourSyncModeAttribute : System.Attribute { public UdonBehaviourSyncModeAttribute(BehaviourSyncMode m) {} }
  public class UdonSharpBehaviour : UnityEngine.MonoBehaviour {
    public virtual void Interact() {}
    public virtual void OnPlayerRestored(VRC.SDKBase.VRCPlayerApi player) {}
  }
}
