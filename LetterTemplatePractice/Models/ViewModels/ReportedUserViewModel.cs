namespace LetterTemplatePractice.Models.ViewModels
{
    public class ReportedUserViewModel
    {
        public int    Id               { get; set; }
        public string Username         { get; set; } = string.Empty;
        public bool   IsHiddenProfile  { get; set; }
        public int    UnresolvedCount  { get; set; }
    }
}
