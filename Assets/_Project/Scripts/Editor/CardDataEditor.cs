using PlushieOdyssey.Data;
using UnityEditor;
using UnityEngine;

namespace PlushieOdyssey.Editor.Inspectors
{
    [CustomEditor(typeof(CardData))]
    [CanEditMultipleObjects]
    public class CardDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _manaProp;
        private SerializedProperty _xBonusProp;
        private SerializedProperty _keywordsProp;

        private void OnEnable()
        {
            _manaProp = serializedObject.FindProperty("<Mana>k__BackingField");
            _xBonusProp = serializedObject.FindProperty("<XBonus>k__BackingField");
            _keywordsProp = serializedObject.FindProperty("<Keywords>k__BackingField");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                if (iterator.name == "<Mana>k__BackingField")
                {
                    DrawManaSection();
                    continue;
                }

                if (iterator.name == "<XBonus>k__BackingField")
                {
                    // Drawn inside DrawManaSection when Is X-Cost is enabled.
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawManaSection()
        {
            if (_manaProp == null || _keywordsProp == null)
            {
                if (_manaProp != null) EditorGUILayout.PropertyField(_manaProp, new GUIContent("Mana"));
                return;
            }

            bool hasXFlag = (_keywordsProp.intValue & (int)CardKeywords.XCost) != 0;
            bool isXCost = hasXFlag || _manaProp.intValue < 0;

            EditorGUI.BeginChangeCheck();
            bool newIsXCost = EditorGUILayout.Toggle(
                new GUIContent(
                    "Is X-Cost",
                    "Displays 'X' instead of a number on the card badge and consumes all remaining Mana when played."),
                isXCost);
            if (EditorGUI.EndChangeCheck())
            {
                if (newIsXCost)
                {
                    _keywordsProp.intValue |= (int)CardKeywords.XCost;
                    _manaProp.intValue = 0;
                }
                else
                {
                    _keywordsProp.intValue &= ~(int)CardKeywords.XCost;
                    if (_manaProp.intValue < 0) _manaProp.intValue = 0;
                }
                isXCost = newIsXCost;
            }

            if (isXCost)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Mana", "X-cost cards display 'X' and spend all remaining Mana."));
                    GUIStyle badgeStyle = new GUIStyle(EditorStyles.textField)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft
                    };
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("X  (Consumes all remaining Mana)", badgeStyle);
                    }
                }

                if (_xBonusProp != null)
                {
                    EditorGUILayout.PropertyField(
                        _xBonusProp,
                        new GUIContent("X Bonus", "Optional bonus added to X when played (e.g. 1 for 'X+1' upgraded cards)."));
                }

                int bonus = _xBonusProp != null ? Mathf.Max(0, _xBonusProp.intValue) : 0;
                string multDesc = bonus > 0 ? $"X + {bonus}" : "X";
                EditorGUILayout.HelpBox(
                    $"Card badge displays 'X' everywhere in-game. Effects scale by ({multDesc}) using all remaining Mana " +
                    "(Damage, Healing, Shield, Draw, Poison/Status stacks, Buff/Debuff value or duration, DoT, Stun, Taunt, Will, and composite effects).",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(_manaProp, new GUIContent("Mana"));
            }
        }
    }
}

