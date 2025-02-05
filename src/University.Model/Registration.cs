namespace University.Model;

[FactType("University.Registration")]
public record Registration(Enrollment enrollment, Offering offering);

[FactType("University.Drop")]
public record Drop(Registration registration);

[FactType("University.Fail")]
public record Fail(Registration registration, int grade);

[FactType("University.Complete")]
public record Complete(Registration registration, int grade);
