public class JsonExtractResult
{
    public JsonExtractResultType Type { get; init; }

    public object Value { get; init; }

    public bool IsNull => Type == JsonExtractResultType.Null;

    public bool IsScalar => Type == JsonExtractResultType.Scalar;

    public bool IsList => Type == JsonExtractResultType.List;

    public override string ToString()
    {
        if (Value == null)
            return "";

        if (Value is List<object> list)
            return "[" + string.Join(", ", list) + "]";

        return Value.ToString();
    }
}
public enum JsonExtractResultType
{
    Null,       // No match or explicit JSON null
    Scalar,     // string / number / bool
    List,       // multiple values
    Object,     // JSON object returned as raw JSON text
    Array       // JSON array returned as raw JSON text
}
