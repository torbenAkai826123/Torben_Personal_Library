
namespace Torben
{
    /// <summary>
    /// Represents a category for logging messages.<br/>
    /// 建立目錄｜分類名稱的類別，這不是 Log 系統必要的類別，主要是為了方便使用者在 Log 中區分不同類型的訊息。<br/>
    /// 例如可以建立不同的 LogCategory 來區分「資訊訊息」、「警告訊息」、「錯誤訊息」等，或是依照模組、功能、流程等自訂分類。<br/>
    /// 這個類別的主要功能是提供一個可辨識的名稱。
    /// </summary>
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

        public string tagColorHex = default;

        // On IdentifierName change, update the tag to reflect the new identifier.
        protected virtual string UpdateTag() => tag = string.IsNullOrEmpty(IdentifierName) ? string.Empty : $"[{IdentifierName}]";

        protected string UpdateTag(string customTag) => tag = string.IsNullOrEmpty(customTag) ? UpdateTag() : customTag;
    }

    // `Empty` log category used as a default when no category is provided.
    internal sealed class LogCategoryEmpty : LogCategory
    {
        public static readonly LogCategoryEmpty None = new LogCategoryEmpty();
        public LogCategoryEmpty() : base("") { }
    }

    public sealed class SystemCategory : LogCategory
    {
        string[] customTags;

        public SystemCategory(object value, params string[] tags) : base(value.GetType().Name)
        {
            customTags = tags;
            UpdateTag();    // 基底建構子呼叫 UpdateTag 時 customTags 尚未指定, 這裡再重建一次
        }

        // 修改 UpdateTag 方法，讓它返回的字串可以改成想要的結果。
        // 分類物件不寫 Log, 避免建立分類時產生訊息
        protected override string UpdateTag()
        {
            string mainTag = IdentifierName;
            if (!string.IsNullOrEmpty(mainTag) && customTags != null && customTags.Length > 0)
            {
                string customTagString = string.Join("][", customTags);
                return base.UpdateTag($"[{mainTag}][{customTagString}]");
            }
            return base.UpdateTag();
        }
    }
}