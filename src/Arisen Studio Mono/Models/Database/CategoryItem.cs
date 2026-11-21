namespace ArisenStudio.Models.Database
{
    public class CategoryItem
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Type { get; set; }

        public string[] Regions { get; set; }

        public CategoryType CategoryType
        {
            get
            {
                switch (Type)
                {
                    case "game":
                        return CategoryType.Game;
                    case "homebrew":
                        return CategoryType.Homebrew;
                    case "resource":
                        return CategoryType.Resource;
                    case "package":
                        return CategoryType.Package;
                    default:
                        return CategoryType.Game;
                }
            }
        }
    }
}
