#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UIPanelSystem.Inspector;

namespace UIPanelSystem.Inspector.Editor
{
    /// <summary>
    /// Translates the package's own inspector attributes into their Odin equivalents.
    ///
    /// This runs only when Odin is installed, and it is the reason the runtime code carries no
    /// Sirenix reference: the attributes stay neutral, and Odin gets told what they mean here.
    /// The fallback inspector (UIPanelInspectorGUI) is compiled out in this configuration so the
    /// two never fight over the same object.
    /// </summary>
    internal sealed class UIPanelOdinAttributeProcessor : OdinAttributeProcessor
    {
        private static readonly Type[] HandledAttributes =
        {
            typeof(BoxGroupAttribute),
            typeof(TabGroupAttribute),
            typeof(FoldoutGroupAttribute),
            typeof(LabelTextAttribute),
            typeof(HideLabelAttribute),
            typeof(InlinePropertyAttribute),
            typeof(ShowIfAttribute),
            typeof(HideIfAttribute),
            typeof(MinMaxSliderAttribute),
            typeof(ButtonAttribute)
        };

        public override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            Type type = property?.ValueEntry?.TypeOfValue;
            return type != null && Attribute.IsDefined(type, typeof(InlinePropertyAttribute), true);
        }

        public override void ProcessSelfAttributes(InspectorProperty property, List<Attribute> attributes)
        {
            if (FindAttribute<Sirenix.OdinInspector.InlinePropertyAttribute>(attributes) == null)
                attributes.Add(new Sirenix.OdinInspector.InlinePropertyAttribute());
        }

        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            if (member == null)
                return false;

            for (int i = 0; i < HandledAttributes.Length; i++)
            {
                if (Attribute.IsDefined(member, HandledAttributes[i], true))
                    return true;
            }

            return false;
        }

        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes)
        {
            AddBoxGroup(attributes);
            AddTabGroup(attributes);
            AddFoldoutGroup(attributes);
            AddLabelText(attributes);
            AddHideLabel(attributes);
            AddInlineProperty(attributes);
            AddShowIf(attributes);
            AddHideIf(attributes);
            AddMinMaxSlider(attributes);
            AddButton(attributes);
        }

        private static void AddBoxGroup(List<Attribute> attributes)
        {
            BoxGroupAttribute attribute = FindAttribute<BoxGroupAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.GroupName))
                return;

            attributes.Add(new Sirenix.OdinInspector.BoxGroupAttribute(attribute.GroupName, attribute.ShowLabel));
        }

        private static void AddTabGroup(List<Attribute> attributes)
        {
            TabGroupAttribute attribute = FindAttribute<TabGroupAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.TabName))
                return;

            Sirenix.OdinInspector.TabGroupAttribute odinAttribute =
                attribute.GroupId == TabGroupAttribute.DefaultGroupId
                    ? new Sirenix.OdinInspector.TabGroupAttribute(attribute.TabName)
                    : new Sirenix.OdinInspector.TabGroupAttribute(attribute.GroupId, attribute.TabName);

            if (!string.IsNullOrEmpty(attribute.TextColor))
                odinAttribute.TextColor = attribute.TextColor;

            attributes.Add(odinAttribute);
        }

        private static void AddFoldoutGroup(List<Attribute> attributes)
        {
            FoldoutGroupAttribute attribute = FindAttribute<FoldoutGroupAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.GroupName))
                return;

            attributes.Add(new Sirenix.OdinInspector.FoldoutGroupAttribute(attribute.GroupName, attribute.Expanded));
        }

        private static void AddLabelText(List<Attribute> attributes)
        {
            LabelTextAttribute attribute = FindAttribute<LabelTextAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.Text))
                return;

            attributes.Add(new Sirenix.OdinInspector.LabelTextAttribute(attribute.Text));
        }

        private static void AddHideLabel(List<Attribute> attributes)
        {
            if (FindAttribute<HideLabelAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.HideLabelAttribute());
        }

        private static void AddInlineProperty(List<Attribute> attributes)
        {
            if (FindAttribute<InlinePropertyAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.InlinePropertyAttribute());
        }

        private static void AddShowIf(List<Attribute> attributes)
        {
            ShowIfAttribute attribute = FindAttribute<ShowIfAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.MemberName))
                return;

            attributes.Add(attribute.ExpectedValue == null
                ? new Sirenix.OdinInspector.ShowIfAttribute(attribute.MemberName)
                : new Sirenix.OdinInspector.ShowIfAttribute(attribute.MemberName, attribute.ExpectedValue));
        }

        private static void AddHideIf(List<Attribute> attributes)
        {
            HideIfAttribute attribute = FindAttribute<HideIfAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.MemberName))
                return;

            attributes.Add(attribute.ExpectedValue == null
                ? new Sirenix.OdinInspector.HideIfAttribute(attribute.MemberName)
                : new Sirenix.OdinInspector.HideIfAttribute(attribute.MemberName, attribute.ExpectedValue));
        }

        private static void AddMinMaxSlider(List<Attribute> attributes)
        {
            MinMaxSliderAttribute attribute = FindAttribute<MinMaxSliderAttribute>(attributes);
            if (attribute == null)
                return;

            attributes.Add(new Sirenix.OdinInspector.MinMaxSliderAttribute(
                attribute.Min, attribute.Max, attribute.ShowFields));
        }

        private static void AddButton(List<Attribute> attributes)
        {
            ButtonAttribute attribute = FindAttribute<ButtonAttribute>(attributes);
            if (attribute == null)
                return;

            attributes.Add(string.IsNullOrEmpty(attribute.Label)
                ? new Sirenix.OdinInspector.ButtonAttribute()
                : new Sirenix.OdinInspector.ButtonAttribute(attribute.Label));
        }

        private static T FindAttribute<T>(List<Attribute> attributes) where T : Attribute
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is T match)
                    return match;
            }

            return null;
        }
    }
}
#endif
