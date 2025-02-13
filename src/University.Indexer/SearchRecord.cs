namespace University.Indexer;

public class SearchRecord
{
    public required string Id { get; init; }
    public required string CourseCode { get; init; }
    public required string CourseName { get; init; }
    public required string Days { get; set; }
    public required string Time { get; set; }
    public required string Instructor { get; set; }
    public required string Location { get; set; }
}
