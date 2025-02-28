public class CourseRecord
{
    public Guid OfferingGuid { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Days { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
