using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Controls;

/// <summary>
/// The editors for the parameters of a request: one per parameter, of the kind its type needs. <see cref="Collect"/> turns
/// what the user entered into an <see cref="ArgumentSet"/>, or says which parameter is wrong.
/// </summary>
internal sealed class ParameterForm : StackPanel
{
    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private readonly ObsSession _session;
    private readonly List<ParameterEditor> _editors = [];

    /// <summary>Initializes a new instance of the <see cref="ParameterForm"/> class.</summary>
    /// <param name="session">The session, for the scenes and inputs to offer.</param>
    public ParameterForm(ObsSession session)
    {
        this._session = session;
        this.Spacing = 4 * Fluent.Unit;
        session.ReferencesChanged += (_, _) =>
        {
            foreach (var editor in this._editors)
            {
                editor.RefreshReferences();
            }
        };
    }

    /// <summary>Gets the number of parameters.</summary>
    public int Count => this._editors.Count;

    /// <summary>Shows the editors for the parameters of a request, empty or with their example.</summary>
    public void Load(IReadOnlyList<ParameterDescriptor> parameters)
    {
        this.Children.Clear();
        this._editors.Clear();
        foreach (var parameter in parameters)
        {
            var editor = this.CreateEditor(parameter);
            this._editors.Add(editor);
            this.Children.Add(editor.Root);
        }
    }

    /// <summary>Reads what the user entered.</summary>
    /// <exception cref="FormatException">A value is not what its parameter needs; the message says which.</exception>
    public ArgumentSet Collect()
    {
        var arguments = new ArgumentSet();
        foreach (var editor in this._editors)
        {
            editor.Collect(arguments);
        }

        return arguments;
    }

    /// <summary>Gives every parameter a value, the optional ones included; for the smoke test.</summary>
    /// <param name="optional">Chooses the optional parameters that get a value; all of them, when null.</param>
    internal void FillForSmokeTest(Func<ParameterDescriptor, bool>? optional = null)
    {
        foreach (var editor in this._editors)
        {
            editor.FillForSmokeTest(optional is null || optional(editor.Descriptor));
        }
    }

    private ParameterEditor CreateEditor(ParameterDescriptor parameter) => parameter.Kind switch
    {
        ParameterKind.Boolean => new ToggleEditor(parameter),
        ParameterKind.Enum when parameter.EnumType?.IsDefined(typeof(FlagsAttribute), false) == true => new FlagsEditor(parameter),
        ParameterKind.Enum => new EnumEditor(parameter),
        ParameterKind.Scene => new ReferenceEditor(parameter, () => this._session.Scenes),
        ParameterKind.Input => new ReferenceEditor(parameter, () => this._session.Inputs),
        ParameterKind.Source => new ReferenceEditor(parameter, () => [.. this._session.Scenes, .. this._session.Inputs]),
        ParameterKind.JsonObject or ParameterKind.Json => new JsonEditor(parameter),
        _ => new TextEditor(parameter),
    };

    private abstract class ParameterEditor
    {
        private readonly CheckBox? _include;

        protected ParameterEditor(ParameterDescriptor descriptor)
        {
            this.Descriptor = descriptor;
            this.Editor = this.CreateEditor();
            AutomationProperties.SetName(this.Editor, descriptor.Name);

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 * Fluent.Unit };
            header.Children.Add(Fluent.Text(descriptor.Name, "BodyStrongTextBlockStyle", wrap: false));
            header.Children.Add(Fluent.Caption(descriptor.Required ? descriptor.TypeName : descriptor.TypeName + " \u00b7 optional"));

            var layout = new StackPanel { Spacing = Fluent.Unit };
            layout.Children.Add(header);
            if (descriptor.Description.Length > 0)
            {
                layout.Children.Add(Fluent.Caption(descriptor.Description));
            }

            if (!descriptor.Required)
            {
                this._include = new CheckBox
                {
                    Content = descriptor.SendsNullWhenOmitted ? "Provide a value (unchecked sends null)" : "Send this parameter",
                    IsChecked = false,
                };
                AutomationProperties.SetName(this._include, $"Send {descriptor.Name}");
                this._include.Checked += (_, _) => this.SetEnabled(true);
                this._include.Unchecked += (_, _) => this.SetEnabled(false);
                this.SetEnabled(false);
                layout.Children.Add(this._include);
            }

            layout.Children.Add(this.Editor);
            this.Root = layout;
        }

        public ParameterDescriptor Descriptor { get; }

        public FrameworkElement Root { get; }

        protected FrameworkElement Editor { get; }

        public void Collect(ArgumentSet arguments)
        {
            if (this._include is not null && this._include.IsChecked != true)
            {
                return;
            }

            arguments.Set(this.Descriptor.Name, this.ReadValue());
        }

        public virtual void RefreshReferences()
        {
        }

        public void FillForSmokeTest(bool includeOptional)
        {
            if (this._include is not null)
            {
                this._include.IsChecked = includeOptional;
                if (!includeOptional)
                {
                    return;
                }
            }

            this.Fill();
        }

        protected abstract FrameworkElement CreateEditor();

        protected abstract void Fill();

        protected abstract object ReadValue();

        protected virtual void SetEnabled(bool enabled)
        {
            if (this.Editor is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.IsEnabled = enabled;
            }
        }

        protected FormatException Problem(string message) => new($"{this.Descriptor.Name}: {message}");
    }

    private sealed class TextEditor : ParameterEditor
    {
        public TextEditor(ParameterDescriptor descriptor)
            : base(descriptor)
        {
        }

        private TextBox Box => (TextBox)this.Editor;

        protected override FrameworkElement CreateEditor() => new TextBox
        {
            PlaceholderText = this.Descriptor.Kind switch
            {
                ParameterKind.Integer => "a whole number",
                ParameterKind.Number => "a number",
                ParameterKind.Guid => "00000000-0000-0000-0000-000000000000",
                _ => "text",
            },
            IsSpellCheckEnabled = false,
        };

        protected override void Fill() => this.Box.Text = this.Descriptor.Kind switch
        {
            ParameterKind.Integer => "1",
            ParameterKind.Number => "0.5",
            ParameterKind.Guid => Guid.NewGuid().ToString(),
            _ => "x",
        };

        protected override object ReadValue()
        {
            var text = this.Box.Text;
            switch (this.Descriptor.Kind)
            {
                case ParameterKind.Integer:
                    return int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                        ? whole
                        : throw this.Problem("enter a whole number, such as 42 or -1.");
                case ParameterKind.Number:
                    return float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? number
                        : throw this.Problem("enter a number, such as 0.5 or -3.");
                case ParameterKind.Guid:
                    return Guid.TryParse(text.Trim(), out var uuid)
                        ? uuid
                        : throw this.Problem("enter a UUID, such as 3f2504e0-4f89-11d3-9a0c-0305e82c3301.");
                default:
                    return text;
            }
        }
    }

    private sealed class ToggleEditor : ParameterEditor
    {
        public ToggleEditor(ParameterDescriptor descriptor)
            : base(descriptor)
        {
        }

        protected override FrameworkElement CreateEditor() => new ToggleSwitch { OnContent = "true", OffContent = "false", IsOn = false };

        protected override void Fill() => ((ToggleSwitch)this.Editor).IsOn = true;

        protected override object ReadValue() => ((ToggleSwitch)this.Editor).IsOn;
    }

    private sealed class EnumEditor : ParameterEditor
    {
        public EnumEditor(ParameterDescriptor descriptor)
            : base(descriptor)
        {
        }

        protected override FrameworkElement CreateEditor() => new ComboBox
        {
            ItemsSource = Enum.GetNames(this.Descriptor.EnumType ?? throw new InvalidOperationException($"{this.Descriptor.Name} has no enumeration.")),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        protected override void Fill()
        {
            // The first member is already selected.
        }

        protected override object ReadValue()
        {
            var type = this.Descriptor.EnumType!;
            return ((ComboBox)this.Editor).SelectedItem is string name
                ? Enum.Parse(type, name)
                : throw this.Problem("choose a value.");
        }
    }

    // An enumeration whose members combine (the modifiers of a key): one check box for each member, and the value is the combination.
    private sealed class FlagsEditor : ParameterEditor
    {
        public FlagsEditor(ParameterDescriptor descriptor)
            : base(descriptor)
        {
        }

        private IEnumerable<CheckBox> Boxes => ((StackPanel)this.Editor).Children.OfType<CheckBox>();

        protected override FrameworkElement CreateEditor()
        {
            var type = this.Descriptor.EnumType ?? throw new InvalidOperationException($"{this.Descriptor.Name} has no enumeration.");
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 * Fluent.Unit };
            foreach (var value in Enum.GetValues(type).Cast<object>())
            {
                if (Convert.ToInt64(value, CultureInfo.InvariantCulture) == 0)
                {
                    continue;
                }

                var box = new CheckBox { Content = value.ToString(), Tag = value, MinWidth = 0 };
                AutomationProperties.SetName(box, $"{this.Descriptor.Name}: {value}");
                panel.Children.Add(box);
            }

            return panel;
        }

        protected override void SetEnabled(bool enabled)
        {
            foreach (var box in this.Boxes)
            {
                box.IsEnabled = enabled;
            }
        }

        protected override void Fill()
        {
            // Two members, so that a combination is what is tried.
            foreach (var box in this.Boxes.Take(2))
            {
                box.IsChecked = true;
            }
        }

        protected override object ReadValue()
        {
            long combined = 0;
            foreach (var box in this.Boxes.Where(box => box.IsChecked == true))
            {
                combined |= Convert.ToInt64(box.Tag, CultureInfo.InvariantCulture);
            }

            return Enum.ToObject(this.Descriptor.EnumType!, combined);
        }
    }

    private sealed class ReferenceEditor : ParameterEditor
    {
        private readonly Func<IReadOnlyList<NamedReference>> _candidates;
        private readonly CheckBox _asUuid = new() { Content = "Send the UUID instead of the name", IsChecked = false };

        public ReferenceEditor(ParameterDescriptor descriptor, Func<IReadOnlyList<NamedReference>> candidates)
            : base(descriptor)
        {
            this._candidates = candidates;
            this.RefreshReferences();
        }

        private ComboBox Combo => (ComboBox)((StackPanel)this.Editor).Children[0];

        public override void RefreshReferences()
        {
            var combo = this.Combo;
            var typed = combo.Text;
            combo.ItemsSource = this._candidates().Select(candidate => candidate.Name).Distinct().ToList();
            combo.Text = typed;
        }

        protected override FrameworkElement CreateEditor()
        {
            var panel = new StackPanel { Spacing = Fluent.Unit };
            var combo = new ComboBox
            {
                IsEditable = true,
                PlaceholderText = "name or UUID: pick one of OBS Studio's, or type",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(combo, this.Descriptor.Name);
            AutomationProperties.SetName(this._asUuid, $"Send the UUID of {this.Descriptor.Name} instead of its name");
            panel.Children.Add(combo);
            panel.Children.Add(this._asUuid);
            return panel;
        }

        protected override void SetEnabled(bool enabled)
        {
            foreach (var child in ((StackPanel)this.Editor).Children)
            {
                if (child is Microsoft.UI.Xaml.Controls.Control control)
                {
                    control.IsEnabled = enabled;
                }
            }
        }

        protected override void Fill() => this.Combo.Text = "name";

        protected override object ReadValue()
        {
            var text = (this.Combo.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw this.Problem("enter a name or a UUID.");
            }

            if (Guid.TryParse(text, out var typedUuid))
            {
                return new ReferenceValue(null, typedUuid);
            }

            if (this._asUuid.IsChecked == true)
            {
                var known = this._candidates().FirstOrDefault(candidate => candidate.Name == text);
                return known?.Uuid is { } uuid
                    ? new ReferenceValue(null, uuid)
                    : throw this.Problem($"'{text}' is not a name that OBS Studio reported, so its UUID is not known. Paste the UUID, or refresh the lists of scenes and inputs.");
            }

            return new ReferenceValue(text, null);
        }
    }

    private sealed class JsonEditor : ParameterEditor
    {
        public JsonEditor(ParameterDescriptor descriptor)
            : base(descriptor)
        {
        }

        protected override FrameworkElement CreateEditor()
        {
            var box = new TextBox
            {
                Text = this.Descriptor.Example ?? "{}",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                IsSpellCheckEnabled = false,
                MinHeight = 24 * 4,
            };
            if (Fluent.StyleOf("CodeTextBoxStyle") is { } style)
            {
                box.Style = style;
            }

            return box;
        }

        protected override void Fill()
        {
            // The example, or {}, is already in the editor.
        }

        protected override object ReadValue()
        {
            try
            {
                using var document = JsonDocument.Parse(((TextBox)this.Editor).Text, Lenient);
                var element = document.RootElement.Clone();
                return this.Descriptor.Kind == ParameterKind.JsonObject && element.ValueKind != JsonValueKind.Object
                    ? throw this.Problem("enter a JSON object, such as {\"name\": \"value\"}.")
                    : element;
            }
            catch (JsonException exception)
            {
                throw this.Problem($"this is not valid JSON ({exception.Message})");
            }
        }
    }
}
