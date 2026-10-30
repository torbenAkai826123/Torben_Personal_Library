
namespace Torben
{
    public abstract class LogCategory
    {
        private string tag;
        private string identifierName;

        protected LogCategory(string identifierName = "") { IdentifierName = identifierName; }

        public string Tag => tag ??= UpdateTag();
        public string IdentifierName
        {
            get => identifierName;
            set
            {
                identifierName = value;
                UpdateTag();
            }
        }

        // On IdentifierName change, update the tag to reflect the new identifier.
        private string UpdateTag() => tag = string.IsNullOrEmpty(IdentifierName) ? string.Empty : $"[{IdentifierName}]";
    }

    // Empty log category used as a default when no category is provided.
    internal sealed class LogCategoryEmpty : LogCategory
    {
        public static readonly LogCategoryEmpty None = new LogCategoryEmpty();
        public LogCategoryEmpty() : base("") { }
    }
}