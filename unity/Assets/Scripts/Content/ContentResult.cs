namespace CatMetro.Content
{
    // A-C2a-3: the importer's failure channel is a typed result, never an exception escaping to
    // the caller (ADR-0008 MUST 3 "never a crash"; CM-R05/CM-R12 need a discriminant to assert).
    public enum ContentErrorKind : byte
    {
        None = 0,
        FileTooLarge = 1,       // pre-parse: CONTENT_MAX_FILE_BYTES
        TooDeep = 2,            // pre-parse: CONTENT_MAX_JSON_DEPTH
        EncodingError = 3,      // not valid UTF-8 (BOM tolerated and stripped)
        MalformedJson = 4,      // truncation, syntax, NaN literals
        DuplicateKey = 5,       // JsonLoadSettings DuplicatePropertyNameHandling.Error
        SchemaVersionMismatch = 6,
        IntegerExpected = 7,    // float/exponent where the schema types an integer
        MissingField = 8,
        BoundViolation = 9,     // a cited ContentBounds range check failed
        CollectionOverCap = 10,
        DuplicateId = 11,
        // Ordinals skip the criterion-5 distinctive-literal set — enum values are arbitrary
        // here and nothing persists them yet.
        DanglingReference = 15,
        PinnedMechanic = 16,    // second source / wild color — names the pin, never throws
    }

    public sealed class ContentError
    {
        public readonly ContentErrorKind Kind;
        public readonly string Detail; // names the offending field/id/pin for assertions and logs

        public ContentError(ContentErrorKind kind, string detail)
        {
            Kind = kind;
            Detail = detail ?? "";
        }

        public override string ToString() => $"{Kind}: {Detail}";
    }

    public readonly struct ContentResult<T>
    {
        public readonly bool Ok;
        public readonly T Value;
        public readonly ContentError Error;

        private ContentResult(bool ok, T value, ContentError error)
        {
            Ok = ok;
            Value = value;
            Error = error;
        }

        public static ContentResult<T> Success(T value) => new ContentResult<T>(true, value, null);
        public static ContentResult<T> Failure(ContentErrorKind kind, string detail) =>
            new ContentResult<T>(false, default, new ContentError(kind, detail));
    }
}
