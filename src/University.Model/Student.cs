namespace University.Model;

[FactType("University.Student")]
public record Student(User user);

[FactType("University.Organization")]
public record Organization(User creator, string identifier);

[FactType("University.Application")]
public record Application(Student student, Organization organization, DateTime appliedAt);

[FactType("University.Enrollment")]
public record Enrollment(Application application);

[FactType("University.Rejection")]
public record Rejection(Application application);
