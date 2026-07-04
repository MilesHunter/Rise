using UnityEditor;

namespace Rise.Editor
{
    [CustomEditor(typeof(ClimbSurface))]
    public sealed class ClimbSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawProperty("surfaceLabelPreset");

            SerializedProperty presetProperty = serializedObject.FindProperty("surfaceLabelPreset");
            if ((ClimbSurfaceLabelPreset)presetProperty.enumValueIndex == ClimbSurfaceLabelPreset.Custom)
            {
                DrawProperty("customSurfaceLabel");
            }

            DrawProperty("surfaceAudio");
            DrawProperty("grabCost");
            DrawProperty("holdDrainPerSecond");
            DrawProperty("slipCheckInterval");
            DrawProperty("slipChance");
            DrawProperty("allowAnchorAttach");
            DrawProperty("allowRopeAttach");
            DrawProperty("gripProbeDepth");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }
    }
}
