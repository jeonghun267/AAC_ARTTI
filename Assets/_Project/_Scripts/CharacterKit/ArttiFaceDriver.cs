using System;
using UnityEngine;

namespace Artti.CharacterKit
{
    [DisallowMultipleComponent]
    public sealed class ArttiFaceDriver : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer[] targets;
        [SerializeField] private bool automaticBlink = true;
        [SerializeField, Tooltip("Enable when an existing lip-sync component writes the mouth shapes directly.")]
        private bool externalLipSync;
        [SerializeField, Min(0)] private float smoothing = 18f;
        [SerializeField, Range(0, 1)] private float smile;
        private static readonly string[] Names = { "A", "E", "I", "O", "U", "JawOpen", "Blink_L", "Blink_R", "Smile", "MouthClose" };
        private readonly float[] goal = new float[10];
        private readonly float[] current = new float[10];
        private int[][] indices;
        private float blinkAt;
        private float blinkPhase = -1;

        public void Bind()
        {
            if (targets == null || targets.Length == 0) targets = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            indices = new int[targets.Length][];
            for (int r = 0; r < targets.Length; ++r)
            {
                indices[r] = new int[Names.Length];
                for (int i = 0; i < Names.Length; ++i)
                    indices[r][i] = targets[r] && targets[r].sharedMesh ? targets[r].sharedMesh.GetBlendShapeIndex(Names[i]) : -1;
            }
            blinkAt = Time.time + 2.8f;
        }
        private void Awake() => Bind();
        public void SetVisemes(float a, float e, float i, float o, float u)
        {
            float[] values = { a, e, i, o, u };
            float sum = 0;
            for (int k = 0; k < 5; ++k) { goal[k] = Mathf.Clamp(values[k], 0, 100); sum += goal[k]; }
            if (sum > 100) for (int k = 0; k < 5; ++k) goal[k] *= 100f / sum;
        }
        public void SetJawOpen(float weight) => goal[5] = Mathf.Clamp(weight, 0, 100);
        public void SetMouthClosure(float weight) => goal[9] = Mathf.Clamp(weight, 0, 100);
        public void SetBlink(float left, float right)
        {
            automaticBlink = false;
            goal[6] = Mathf.Clamp(left, 0, 100); goal[7] = Mathf.Clamp(right, 0, 100);
        }
        public void SetSmile(float weight) => smile = Mathf.Clamp01(weight / 100f);
        public void SetExternalLipSync(bool enabled) => externalLipSync = enabled;
        public void SetAutomaticBlink(bool enabled) { automaticBlink = enabled; blinkPhase = -1; blinkAt = Time.time + 2.8f; }
        public void Clear()
        {
            Array.Clear(goal, 0, goal.Length); Array.Clear(current, 0, current.Length); smile = 0;
            ApplyImmediate();
        }
        public void ApplyImmediate()
        {
            if (indices == null) Bind();
            Array.Copy(goal, current, goal.Length); current[8] = smile * 100f; Write();
        }
        private void LateUpdate()
        {
            if (indices == null) Bind();
            if (automaticBlink)
            {
                if (blinkPhase < 0 && Time.time >= blinkAt) blinkPhase = 0;
                if (blinkPhase >= 0)
                {
                    blinkPhase += Time.deltaTime;
                    float t = blinkPhase / .22f;
                    goal[6] = goal[7] = t < 1 ? Mathf.Sin(t * Mathf.PI) * 100 : 0;
                    if (t >= 1) { blinkPhase = -1; blinkAt = Time.time + UnityEngine.Random.Range(2.8f, 5.2f); }
                }
            }
            goal[8] = smile * 100f;
            float k = smoothing <= 0 ? 1 : 1 - Mathf.Exp(-smoothing * Time.deltaTime);
            for (int i = 0; i < Names.Length; ++i) current[i] = Mathf.Lerp(current[i], goal[i], i == 6 || i == 7 ? 1 : k);
            Write();
        }
        private void Write()
        {
            for (int r = 0; r < targets.Length; ++r)
                if (targets[r]) for (int i = 0; i < Names.Length; ++i)
                    if (indices[r][i] >= 0 && !(externalLipSync && (i < 6 || i == 9))) targets[r].SetBlendShapeWeight(indices[r][i], current[i]);
        }
    }
}
