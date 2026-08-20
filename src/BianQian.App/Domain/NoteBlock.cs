using System.Text.Json.Serialization;

namespace BianQian.App.Domain;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(TaskBlock), "task")]
public abstract class NoteBlock
{
    protected NoteBlock()
    {
    }
}
