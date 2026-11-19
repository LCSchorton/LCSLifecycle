-- Apply once to a dedicated, empty LCS test database. Run with sqlcmd -b.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE dbo.RosterBatch (
    Id bigint IDENTITY PRIMARY KEY,
    Source varchar(16) NOT NULL CHECK (Source IN ('FACTS', 'CSV')),
    ReceivedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Scope nvarchar(100) NOT NULL,
    [RowCount] int NOT NULL CHECK ([RowCount] >= 0),
    ValidationStatus varchar(16) NOT NULL CHECK (ValidationStatus IN ('Pending', 'Validated', 'Rejected')),
    IsComplete bit NOT NULL DEFAULT 0
);

CREATE TABLE dbo.Student (
    Id bigint IDENTITY PRIMARY KEY,
    FactsStudentId nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE,
    GivenName nvarchar(100) NULL,
    Surname nvarchar(100) NULL,
    Version rowversion NOT NULL
);

CREATE TABLE dbo.EnrollmentEvidence (
    Id bigint IDENTITY PRIMARY KEY,
    StudentId bigint NOT NULL REFERENCES dbo.Student(Id),
    RosterBatchId bigint NULL REFERENCES dbo.RosterBatch(Id),
    Status varchar(16) NOT NULL CHECK (Status IN ('Enrolled','Withdrawn','Graduated')),
    EffectiveDate date NOT NULL,
    RecordedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ConfirmedByStaffObjectId uniqueidentifier NULL,
    SourceReference nvarchar(200) NOT NULL,
    CONSTRAINT UQ_EnrollmentEvidence_Student UNIQUE (StudentId, Id),
    CONSTRAINT CK_EnrollmentEvidence_Source CHECK (RosterBatchId IS NOT NULL OR ConfirmedByStaffObjectId IS NOT NULL)
);

CREATE TABLE dbo.StudentIdentity (
    EntraObjectId uniqueidentifier NOT NULL PRIMARY KEY,
    StudentId bigint NOT NULL REFERENCES dbo.Student(Id),
    UserPrincipalName nvarchar(254) NULL,
    LinkedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    EndedUtc datetime2(3) NULL,
    CHECK (EndedUtc IS NULL OR EndedUtc >= LinkedUtc)
);
CREATE UNIQUE INDEX UX_StudentIdentity_Current
    ON dbo.StudentIdentity(StudentId) WHERE EndedUtc IS NULL;

CREATE TABLE dbo.Device (
    Id bigint IDENTITY PRIMARY KEY,
    SerialNumber varchar(64) NOT NULL UNIQUE,
    Model nvarchar(100) NULL,
    AssetTag nvarchar(64) NULL,
    Condition varchar(20) NOT NULL DEFAULT 'Unknown'
        CHECK (Condition IN ('Unknown','Working','Damaged','Nonfunctional')),
    LifecycleStatus varchar(20) NOT NULL DEFAULT 'Unknown'
        CHECK (LifecycleStatus IN ('Unknown','Available','Assigned','Repair','Lost','Stolen','Retired','Transferred')),
    Version rowversion NOT NULL
);
CREATE UNIQUE INDEX UX_Device_AssetTag ON dbo.Device(AssetTag) WHERE AssetTag IS NOT NULL;

CREATE TABLE dbo.DeviceObservation (
    Id bigint IDENTITY PRIMARY KEY,
    DeviceId bigint NOT NULL REFERENCES dbo.Device(Id),
    Source varchar(16) NOT NULL CHECK (Source IN ('Apple','Securly')),
    SourceDeviceId nvarchar(128) NULL,
    ObservedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Registered bit NULL,
    MdmService nvarchar(128) NULL,
    Enrolled bit NULL,
    LastCheckInUtc datetime2(3) NULL
);
CREATE INDEX IX_DeviceObservation_Latest ON dbo.DeviceObservation(DeviceId, Source, ObservedUtc DESC);

CREATE TABLE dbo.DeviceAssignment (
    Id bigint IDENTITY PRIMARY KEY,
    DeviceId bigint NOT NULL REFERENCES dbo.Device(Id),
    StudentId bigint NOT NULL REFERENCES dbo.Student(Id),
    AssignedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    AssignedBy uniqueidentifier NOT NULL,
    EndedUtc datetime2(3) NULL,
    EndReason varchar(20) NULL,
    CHECK ((EndedUtc IS NULL AND EndReason IS NULL) OR
        (EndedUtc IS NOT NULL AND EndReason IS NOT NULL AND EndedUtc >= AssignedUtc AND
          EndReason IN ('Returned','Replaced','Lost','Stolen','Transferred','Correction')))
);
CREATE UNIQUE INDEX UX_DeviceAssignment_CurrentDevice ON dbo.DeviceAssignment(DeviceId) WHERE EndedUtc IS NULL;
CREATE INDEX IX_DeviceAssignment_Student ON dbo.DeviceAssignment(StudentId, AssignedUtc DESC);

CREATE TABLE dbo.DeviceOwnershipEvent (
    Id bigint IDENTITY PRIMARY KEY,
    DeviceId bigint NOT NULL REFERENCES dbo.Device(Id),
    Ownership varchar(20) NOT NULL CHECK (Ownership IN ('School','Family','Unknown')),
    RecordedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    StaffObjectId uniqueidentifier NOT NULL
);

CREATE TABLE dbo.WorkflowCase (
    Id bigint IDENTITY PRIMARY KEY,
    StudentId bigint NOT NULL REFERENCES dbo.Student(Id),
    EnrollmentEvidenceId bigint NULL,
    CaseType varchar(20) NOT NULL CHECK (CaseType IN ('Onboarding','Offboarding','Assignment','Replacement','DeviceIncident')),
    OpenedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ClosedUtc datetime2(3) NULL,
    CONSTRAINT FK_WorkflowCase_EnrollmentEvidence FOREIGN KEY (StudentId, EnrollmentEvidenceId)
        REFERENCES dbo.EnrollmentEvidence(StudentId, Id),
    Version rowversion NOT NULL
);
CREATE UNIQUE INDEX UX_WorkflowCase_OpenDeparture ON dbo.WorkflowCase(StudentId)
    WHERE CaseType = 'Offboarding' AND ClosedUtc IS NULL;

CREATE TABLE dbo.WorkflowStep (
    CaseId bigint NOT NULL REFERENCES dbo.WorkflowCase(Id),
    StepCode varchar(50) NOT NULL,
    Mode varchar(10) NOT NULL CHECK (Mode IN ('Manual','Automatic')),
    Status varchar(16) NOT NULL CHECK (Status IN ('Pending','Completed','Blocked','Skipped')),
    CompletedUtc datetime2(3) NULL,
    CompletedBy uniqueidentifier NULL,
    PRIMARY KEY (CaseId, StepCode)
);

CREATE TABLE dbo.AuditEvent (
    Id bigint IDENTITY PRIMARY KEY,
    OccurredUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CorrelationId uniqueidentifier NOT NULL,
    ActorType varchar(10) NOT NULL CHECK (ActorType IN ('Staff', 'Service')),
    ActorObjectId uniqueidentifier NULL,
    CaseId bigint NULL REFERENCES dbo.WorkflowCase(Id),
    EventCode varchar(60) NOT NULL,
    Outcome varchar(20) NOT NULL
);
COMMIT TRANSACTION;
