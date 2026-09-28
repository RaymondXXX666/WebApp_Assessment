namespace CreditWorks.Web.Services;

public class CategoryDraft
{
    // 现有分类有 Id；新加的分类用 0。
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // 用户从现有图标中选择一个文件名。
    public string IconName { get; set; } = string.Empty;

    public decimal? MinWeightKg { get; set; }

    // null 表示没有上限，只允许最后一个分类这样设置。
    public decimal? MaxWeightKg { get; set; }
}
