using UnityEngine;

namespace Artti.CharacterKit
{
    public sealed class ArttiDemoControls : MonoBehaviour
    {
        public ArttiAnimationDriver body;
        public ArttiFaceDriver face;
        public ArttiAudioLipSync speech;
        [Tooltip("Show only Idle, Wave and facial/audio controls for the stationary character package.")]
        public bool stationaryOnly;
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 180, 440), GUI.skin.box);
            GUILayout.Label("ARTTI Character Preview");
            if(speech) {
                if(GUILayout.Button("Speak Korean"))speech.PlayDemo();
                if(GUILayout.Button("Stop speech"))speech.Stop();
                GUILayout.Label("Phoneme: "+speech.CurrentPhoneme);
            }
            if (body)
            {
                if (GUILayout.Button("Idle")) body.Idle();
                if (GUILayout.Button("Wave")) body.Wave();
                if (!stationaryOnly) {
                    if (GUILayout.Button("Walk")) body.Walk();
                    if (GUILayout.Button("Run")) body.Run();
                }
            }
            if (face)
            {
                if (GUILayout.Button("Smile")) face.SetSmile(70);
                if (GUILayout.Button("A")) face.SetVisemes(70, 0, 0, 0, 0);
                if (GUILayout.Button("E")) face.SetVisemes(0, 70, 0, 0, 0);
                if (GUILayout.Button("I")) face.SetVisemes(0, 0, 70, 0, 0);
                if (GUILayout.Button("O")) face.SetVisemes(0, 0, 0, 70, 0);
                if (GUILayout.Button("U")) face.SetVisemes(0, 0, 0, 0, 70);
                if (GUILayout.Button("Neutral")) face.Clear();
            }
            GUILayout.EndArea();
        }
    }
}
