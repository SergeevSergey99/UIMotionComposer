using System;

namespace UIPanelSystem.Inspector
{
    /// <summary>
    /// Inspector attributes owned by the UI panel package.
    ///
    /// They are plain attributes with no dependency on any asset store plugin, so the runtime code
    /// compiles in every project. Two editor-side implementations pick them up:
    ///   * with Odin installed, UIPanelOdinBridge translates each one into its Sirenix counterpart;
    ///   * without Odin, UIPanelInspectorGUI draws them itself.
    /// Anything Odin can do that the fallback cannot (expression driven colors, for instance) is
    /// carried as an optional extra rather than being required to read the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class BoxGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public bool ShowLabel { get; set; } = true;

        public BoxGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }

    /// <summary>
    /// Puts the field on a tab. Fields sharing <see cref="GroupId"/> form one tab bar, in the order
    /// their tabs are first declared.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class TabGroupAttribute : Attribute
    {
        public const string DefaultGroupId = "_DefaultTabGroup";

        public string GroupId { get; }
        public string TabName { get; }

        /// <summary>
        /// Odin style color expression, e.g. "@this.Alpha.AnimationColor". The fallback inspector
        /// understands the "@this.Member.Member" form and ignores anything more complex.
        /// </summary>
        public string TextColor { get; set; }

        public TabGroupAttribute(string tabName)
            : this(DefaultGroupId, tabName)
        {
        }

        public TabGroupAttribute(string groupId, string tabName)
        {
            GroupId = groupId;
            TabName = tabName;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class FoldoutGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public bool Expanded { get; }

        public FoldoutGroupAttribute(string groupName, bool expanded = true)
        {
            GroupName = groupName;
            Expanded = expanded;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class LabelTextAttribute : Attribute
    {
        public string Text { get; }

        public LabelTextAttribute(string text)
        {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class HideLabelAttribute : Attribute
    {
    }

    /// <summary>Draws a serializable class inline, without its own foldout.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, Inherited = true)]
    public sealed class InlinePropertyAttribute : Attribute
    {
    }

    /// <summary>
    /// Shows the field while the referenced member is true, or equals <see cref="ExpectedValue"/>.
    /// The member may be a field, a property or a parameterless method on the declaring object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ShowIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object ExpectedValue { get; }

        public ShowIfAttribute(string memberName)
        {
            MemberName = memberName;
        }

        public ShowIfAttribute(string memberName, object expectedValue)
        {
            MemberName = memberName;
            ExpectedValue = expectedValue;
        }
    }

    /// <summary>Inverse of <see cref="ShowIfAttribute"/>.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class HideIfAttribute : Attribute
    {
        public string MemberName { get; }
        public object ExpectedValue { get; }

        public HideIfAttribute(string memberName)
        {
            MemberName = memberName;
        }

        public HideIfAttribute(string memberName, object expectedValue)
        {
            MemberName = memberName;
            ExpectedValue = expectedValue;
        }
    }

    /// <summary>Two handled range slider over a Vector2, x being the low end and y the high end.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class MinMaxSliderAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }
        public bool ShowFields { get; }

        public MinMaxSliderAttribute(float min, float max, bool showFields = false)
        {
            Min = min;
            Max = max;
            ShowFields = showFields;
        }
    }

    /// <summary>Draws a button that invokes the method it is attached to.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Label { get; }

        public ButtonAttribute()
        {
        }

        public ButtonAttribute(string label)
        {
            Label = label;
        }
    }
}
