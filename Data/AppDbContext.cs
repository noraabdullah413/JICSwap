// Source: database/migrations/2026_10_13_000001_create_administrators_table.php … 2026_10_13_000015_create_queue_tickets_table.php
// Source: app/Models/*.php (Eloquent relations) — configured here in OnModelCreating
using JicRss.Models;
using Microsoft.EntityFrameworkCore;

namespace JicRss.Data;

/// <summary>
/// DbSets + Fluent configuration of the 15 ERD tables (Blueprint §4 — exactly 15 tables).
/// The full CREATE TABLE script lives in jicrss.sql (same names, types, FKs, indexes, checks).
/// Connection string = DB_* keys of .env -> appsettings.json "ConnectionStrings:DefaultConnection" (SQL Server).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ---- tables (migration order)
    public DbSet<Administrator> Administrators => Set<Administrator>();                         // 000001
    public DbSet<Department> Departments => Set<Department>();                                  // 000002
    public DbSet<Student> Students => Set<Student>();                                           // 000003
    public DbSet<AcademicAdvisor> AcademicAdvisors => Set<AcademicAdvisor>();                   // 000004
    public DbSet<HeadOfDepartment> HeadsOfDepartment => Set<HeadOfDepartment>();                // 000005
    public DbSet<SchedulingCoordinator> SchedulingCoordinators => Set<SchedulingCoordinator>(); // 000006
    public DbSet<Course> Courses => Set<Course>();                                              // 000007
    // 000008 course_prerequisites: bridge table (composite key, no extra columns) — Course.Prerequisites / Course.RequiredBy
    public DbSet<Section> Sections => Set<Section>();                                           // 000009
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();                                  // 000010
    public DbSet<NormalRequest> NormalRequests => Set<NormalRequest>();                         // 000011
    public DbSet<SpecialRequest> SpecialRequests => Set<SpecialRequest>();                      // 000012
    public DbSet<SpecialRequestDocument> SpecialRequestDocuments => Set<SpecialRequestDocument>(); // 000013
    public DbSet<RequestStatusHistory> RequestStatusHistory => Set<RequestStatusHistory>();     // 000014
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();                               // 000015

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Account is the abstract base of the five actor tables (not a table itself).
        b.Ignore<Account>();

        // ------------------------------------------------------------ 000001 administrators
        b.Entity<Administrator>(e =>
        {
            e.ToTable("administrators", t =>
            {
                t.HasCheckConstraint("CK_admin_email", "admin_email LIKE '%_@jic.edu.sa'");
                t.HasCheckConstraint("CK_admin_phone", "admin_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' OR admin_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
            });
            e.HasKey(x => x.AdminId).HasName("PK_administrators");
            e.Property(x => x.AdminStaffId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.AdminFname).HasMaxLength(50);
            e.Property(x => x.AdminLname).HasMaxLength(50);
            e.Property(x => x.AdminEmail).HasMaxLength(120);
            e.Property(x => x.AdminPassword).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.AdminPhone).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.AdminIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.AdminStaffId).IsUnique().HasDatabaseName("UQ_admin_staff");
            e.HasIndex(x => x.AdminEmail).IsUnique().HasDatabaseName("UQ_admin_email");
        });

        // ------------------------------------------------------------ 000002 departments
        b.Entity<Department>(e =>
        {
            e.ToTable("departments", t => t.HasCheckConstraint("CK_dept_email", "dept_email LIKE '%_@jic.edu.sa'"));
            e.HasKey(x => x.DeptId).HasName("PK_departments");
            e.Property(x => x.DeptCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.DeptName).HasMaxLength(100);
            e.Property(x => x.DeptEmail).HasMaxLength(120);
            e.Property(x => x.DeptPhone).HasMaxLength(12).IsUnicode(false);
            e.HasIndex(x => x.DeptCode).IsUnique().HasDatabaseName("UQ_dept_code");
            e.HasIndex(x => x.DeptName).IsUnique().HasDatabaseName("UQ_dept_name");
            // administrator(): belongsTo / departments(): hasMany  — FK_dept_admin
            e.HasOne(x => x.Administrator).WithMany(a => a.Departments).HasForeignKey(x => x.AdminId)
                .HasConstraintName("FK_dept_admin").OnDelete(DeleteBehavior.NoAction);
            // head(): hasOne — UQ_hod_dept (one HOD per department)
            e.HasOne(x => x.Head).WithOne(h => h.Department).HasForeignKey<HeadOfDepartment>(h => h.DeptId)
                .HasConstraintName("FK_hod_dept").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000003 students
        b.Entity<Student>(e =>
        {
            e.ToTable("students", t =>
            {
                t.HasCheckConstraint("CK_student_univ", "student_university_id NOT LIKE '%[^0-9]%'");
                t.HasCheckConstraint("CK_student_email", "student_email LIKE '%_@jic.edu.sa'");
                t.HasCheckConstraint("CK_student_phone", "student_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' OR student_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
                t.HasCheckConstraint("CK_student_level", "student_academic_level BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_student_gpa", "student_gpa IS NULL OR student_gpa BETWEEN 0 AND 4");
                t.HasCheckConstraint("CK_student_hours", "student_completed_hours IS NULL OR student_completed_hours BETWEEN 0 AND 200");
            });
            e.HasKey(x => x.StudentId).HasName("PK_students");
            e.Property(x => x.StudentUniversityId).HasMaxLength(9).IsFixedLength().IsUnicode(false);
            e.Property(x => x.StudentFname).HasMaxLength(50);
            e.Property(x => x.StudentLname).HasMaxLength(50);
            e.Property(x => x.StudentEmail).HasMaxLength(120);
            e.Property(x => x.StudentPassword).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.StudentPhone).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.StudentIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.StudentUniversityId).IsUnique().HasDatabaseName("UQ_student_univ");
            e.HasIndex(x => x.StudentEmail).IsUnique().HasDatabaseName("UQ_student_email");
            e.HasIndex(x => x.StudentPhone).IsUnique().HasDatabaseName("UQ_student_phone");
            e.HasIndex(x => x.DeptId).HasDatabaseName("IX_students_dept");
            e.HasOne(x => x.Department).WithMany(d => d.Students).HasForeignKey(x => x.DeptId)
                .HasConstraintName("FK_student_dept").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000004 academic_advisors
        b.Entity<AcademicAdvisor>(e =>
        {
            e.ToTable("academic_advisors", t =>
            {
                t.HasCheckConstraint("CK_advisor_email", "advisor_email LIKE '%_@jic.edu.sa'");
                t.HasCheckConstraint("CK_advisor_phone", "advisor_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' OR advisor_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
            });
            e.HasKey(x => x.AdvisorId).HasName("PK_academic_advisors");
            e.Property(x => x.AdvisorStaffId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.AdvisorFname).HasMaxLength(50);
            e.Property(x => x.AdvisorLname).HasMaxLength(50);
            e.Property(x => x.AdvisorEmail).HasMaxLength(120);
            e.Property(x => x.AdvisorPassword).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.AdvisorPhone).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.AdvisorIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.AdvisorStaffId).IsUnique().HasDatabaseName("UQ_advisor_staff");
            e.HasIndex(x => x.AdvisorEmail).IsUnique().HasDatabaseName("UQ_advisor_email");
            e.HasOne(x => x.Department).WithMany(d => d.Advisors).HasForeignKey(x => x.DeptId)
                .HasConstraintName("FK_advisor_dept").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000005 heads_of_department
        b.Entity<HeadOfDepartment>(e =>
        {
            e.ToTable("heads_of_department", t =>
            {
                t.HasCheckConstraint("CK_hod_email", "hod_email LIKE '%_@jic.edu.sa'");
                t.HasCheckConstraint("CK_hod_phone", "hod_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' OR hod_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
            });
            e.HasKey(x => x.HodId).HasName("PK_heads_of_department");
            e.Property(x => x.HodStaffId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.HodFname).HasMaxLength(50);
            e.Property(x => x.HodLname).HasMaxLength(50);
            e.Property(x => x.HodEmail).HasMaxLength(120);
            e.Property(x => x.HodPassword).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.HodPhone).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.HodIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.HodStaffId).IsUnique().HasDatabaseName("UQ_hod_staff");
            e.HasIndex(x => x.HodEmail).IsUnique().HasDatabaseName("UQ_hod_email");
            e.HasIndex(x => x.DeptId).IsUnique().HasDatabaseName("UQ_hod_dept");   // a department is headed by one HOD
        });

        // ------------------------------------------------------------ 000006 scheduling_coordinators
        b.Entity<SchedulingCoordinator>(e =>
        {
            e.ToTable("scheduling_coordinators", t =>
            {
                t.HasCheckConstraint("CK_coord_email", "coord_email LIKE '%_@jic.edu.sa'");
                t.HasCheckConstraint("CK_coord_phone", "coord_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' OR coord_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
            });
            e.HasKey(x => x.CoordId).HasName("PK_scheduling_coordinators");
            e.Property(x => x.CoordStaffId).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.CoordFname).HasMaxLength(50);
            e.Property(x => x.CoordLname).HasMaxLength(50);
            e.Property(x => x.CoordEmail).HasMaxLength(120);
            e.Property(x => x.CoordPassword).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.CoordPhone).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.CoordIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.CoordStaffId).IsUnique().HasDatabaseName("UQ_coord_staff");
            e.HasIndex(x => x.CoordEmail).IsUnique().HasDatabaseName("UQ_coord_email");
            e.HasOne(x => x.Department).WithMany(d => d.Coordinators).HasForeignKey(x => x.DeptId)
                .HasConstraintName("FK_coord_dept").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000007 courses + 000008 course_prerequisites
        b.Entity<Course>(e =>
        {
            e.ToTable("courses", t =>
            {
                t.HasCheckConstraint("CK_course_hours", "course_credit_hours BETWEEN 0 AND 6");
                t.HasCheckConstraint("CK_course_level", "course_level BETWEEN 1 AND 10");
            });
            e.HasKey(x => x.CourseId).HasName("PK_courses");
            e.Property(x => x.CourseCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.CourseName).HasMaxLength(120);
            e.Property(x => x.CourseIsActive).HasDefaultValue(true);
            e.HasIndex(x => x.CourseCode).IsUnique().HasDatabaseName("UQ_course_code");
            e.HasIndex(x => x.DeptId).HasDatabaseName("IX_courses_dept");
            e.HasOne(x => x.Department).WithMany(d => d.Courses).HasForeignKey(x => x.DeptId)
                .HasConstraintName("FK_course_dept").OnDelete(DeleteBehavior.NoAction);

            // prerequisites(): belongsToMany(Course, 'course_prerequisites', 'course_id', 'prereq_course_id')
            // requiredBy():    belongsToMany(Course, 'course_prerequisites', 'prereq_course_id', 'course_id')
            e.HasMany(x => x.Prerequisites).WithMany(x => x.RequiredBy).UsingEntity<Dictionary<string, object>>(
                "course_prerequisites",
                r => r.HasOne<Course>().WithMany().HasForeignKey("prereq_course_id").HasConstraintName("FK_prereq_prereq").OnDelete(DeleteBehavior.NoAction),
                l => l.HasOne<Course>().WithMany().HasForeignKey("course_id").HasConstraintName("FK_prereq_course").OnDelete(DeleteBehavior.NoAction),
                j =>
                {
                    j.ToTable("course_prerequisites", t => t.HasCheckConstraint("CK_prereq_not_self", "course_id <> prereq_course_id"));
                    j.HasKey("course_id", "prereq_course_id").HasName("PK_course_prerequisites");
                });
        });

        // ------------------------------------------------------------ 000009 sections
        b.Entity<Section>(e =>
        {
            e.ToTable("sections", t =>
            {
                t.HasCheckConstraint("CK_section_time", "section_end_time > section_start_time");
                t.HasCheckConstraint("CK_section_seats", "section_capacity > 0 AND section_enrolled BETWEEN 0 AND section_capacity");
                t.HasCheckConstraint("CK_section_status", "section_status IN ('Open','Full','Closed','Cancelled')");
            });
            e.HasKey(x => x.SectionId).HasName("PK_sections");
            e.Property(x => x.SectionNumber).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.SectionSemester).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.SectionDays).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.SectionRoom).HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.SectionInstructor).HasMaxLength(100);
            e.Property(x => x.SectionEnrolled).HasDefaultValue((short)0);
            e.Property(x => x.SectionStatus).HasMaxLength(10).IsUnicode(false).HasDefaultValue("Open");
            e.HasIndex(x => new { x.CourseId, x.SectionSemester, x.SectionNumber }).IsUnique().HasDatabaseName("UQ_section");
            e.HasOne(x => x.Course).WithMany(c => c.Sections).HasForeignKey(x => x.CourseId)
                .HasConstraintName("FK_section_course").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000010 enrollments
        b.Entity<Enrollment>(e =>
        {
            e.ToTable("enrollments", t => t.HasCheckConstraint("CK_enroll_status", "enroll_status IN ('Registered','Dropped')"));
            e.HasKey(x => x.EnrollId).HasName("PK_enrollments");
            e.Property(x => x.EnrollSemester).HasMaxLength(5).IsUnicode(false);
            e.Property(x => x.EnrollStatus).HasMaxLength(12).IsUnicode(false).HasDefaultValue("Registered");
            e.Property(x => x.EnrollRegisteredDate).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => new { x.StudentId, x.SectionId }).IsUnique().HasDatabaseName("UX_enroll_active")
                .HasFilter("[enroll_status] = 'Registered'");
            e.HasIndex(x => x.SectionId).HasDatabaseName("IX_enroll_section");
            e.HasOne(x => x.Student).WithMany(s => s.Enrollments).HasForeignKey(x => x.StudentId)
                .HasConstraintName("FK_enroll_student").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Section).WithMany(s => s.Enrollments).HasForeignKey(x => x.SectionId)
                .HasConstraintName("FK_enroll_section").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Coordinator).WithMany().HasForeignKey(x => x.CoordId)
                .HasConstraintName("FK_enroll_coord").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000011 normal_requests
        b.Entity<NormalRequest>(e =>
        {
            e.ToTable("normal_requests", t =>
            {
                t.HasCheckConstraint("CK_nreq_status", "nreq_status IN ('Draft','New','Under Process','Completed','Rejected','Cancelled')");
                t.HasCheckConstraint("CK_nreq_priority", "nreq_priority IN ('High','Medium','Low')");
                t.HasCheckConstraint("CK_nreq_reason", "nreq_reason IN ('Section is full','Time conflict','Course missing from my schedule','Missed my registration day','Other')");
                t.HasCheckConstraint("CK_nreq_desc", "LEN(nreq_description) >= 30");
                t.HasCheckConstraint("CK_nreq_sections", "current_section_id IS NULL OR requested_section_id IS NULL OR current_section_id <> requested_section_id");
                t.HasCheckConstraint("CK_nreq_submitted", "nreq_status = 'Draft' OR nreq_submitted_date IS NOT NULL");
                t.HasCheckConstraint("CK_nreq_closed", "nreq_status NOT IN ('Completed','Rejected','Cancelled') OR nreq_closed_date IS NOT NULL");
            });
            e.HasKey(x => x.NreqId).HasName("PK_normal_requests");
            e.Property(x => x.NreqNumber).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.NreqDescription).HasMaxLength(500);
            e.Property(x => x.NreqStatus).HasMaxLength(15).IsUnicode(false).HasDefaultValue("Draft");
            e.Property(x => x.NreqPriority).HasMaxLength(10).IsUnicode(false).HasDefaultValue("Medium");
            e.Property(x => x.NreqCoordinatorComment).HasMaxLength(500);
            e.Property(x => x.NreqReason).HasMaxLength(40).IsUnicode(false);
            e.HasIndex(x => x.NreqNumber).IsUnique().HasDatabaseName("UQ_nreq_number");
            e.HasIndex(x => x.StudentId).HasDatabaseName("IX_nreq_student");
            e.HasIndex(x => new { x.NreqStatus, x.NreqPriority }).HasDatabaseName("IX_nreq_status");
            e.HasOne(x => x.Student).WithMany(s => s.NormalRequests).HasForeignKey(x => x.StudentId)
                .HasConstraintName("FK_nreq_student").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId)
                .HasConstraintName("FK_nreq_course").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.CurrentSection).WithMany().HasForeignKey(x => x.CurrentSectionId)
                .HasConstraintName("FK_nreq_current").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.RequestedSection).WithMany().HasForeignKey(x => x.RequestedSectionId)
                .HasConstraintName("FK_nreq_requested").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Coordinator).WithMany().HasForeignKey(x => x.CoordId)
                .HasConstraintName("FK_nreq_coord").OnDelete(DeleteBehavior.NoAction);
            // IsRequest::history(): hasMany(RequestStatusHistory, 'nreq_id', 'nreq_id')
            e.HasMany(x => x.History).WithOne(h => h.NormalRequest).HasForeignKey(h => h.NreqId)
                .HasConstraintName("FK_hist_nreq").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000012 special_requests
        b.Entity<SpecialRequest>(e =>
        {
            e.ToTable("special_requests", t =>
            {
                t.HasCheckConstraint("CK_sreq_case", "sreq_case_type IN ('Prerequisite Exemption Form','Course Substitution','Credit Overload','Graduation Exception','Late Withdrawal')");
                t.HasCheckConstraint("CK_sreq_status", "sreq_status IN ('Draft','With Advisor','With Head of Department','Accepted','Completed','Rejected','Cancelled')");
                t.HasCheckConstraint("CK_sreq_priority", "sreq_priority IN ('High','Medium','Low')");
                t.HasCheckConstraint("CK_sreq_adv_dec", "sreq_advisor_decision IS NULL OR sreq_advisor_decision IN ('Accepted','Rejected')");
                t.HasCheckConstraint("CK_sreq_hod_dec", "sreq_hod_decision IS NULL OR sreq_hod_decision IN ('Accepted','Rejected')");
                t.HasCheckConstraint("CK_sreq_desc", "LEN(sreq_description) >= 30");
                // a decision always comes with its date and the deciding person
                t.HasCheckConstraint("CK_sreq_adv_trio", "(sreq_advisor_decision IS NULL AND sreq_advisor_decision_date IS NULL) OR (sreq_advisor_decision IS NOT NULL AND sreq_advisor_decision_date IS NOT NULL AND advisor_id IS NOT NULL)");
                t.HasCheckConstraint("CK_sreq_hod_trio", "(sreq_hod_decision IS NULL AND sreq_hod_decision_date IS NULL) OR (sreq_hod_decision IS NOT NULL AND sreq_hod_decision_date IS NOT NULL AND hod_id IS NOT NULL)");
                // the head of department decides only after the advisor accepted
                t.HasCheckConstraint("CK_sreq_order", "sreq_hod_decision IS NULL OR sreq_advisor_decision = 'Accepted'");
                // a rejection needs a written reason of at least 20 characters (UC2-06)
                t.HasCheckConstraint("CK_sreq_adv_why", "sreq_advisor_decision IS NULL OR sreq_advisor_decision = 'Accepted' OR LEN(sreq_advisor_comment) >= 20");
                t.HasCheckConstraint("CK_sreq_hod_why", "sreq_hod_decision IS NULL OR sreq_hod_decision = 'Accepted' OR LEN(sreq_hod_comment) >= 20");
                t.HasCheckConstraint("CK_sreq_submitted", "sreq_status = 'Draft' OR sreq_submitted_date IS NOT NULL");
            });
            e.HasKey(x => x.SreqId).HasName("PK_special_requests");
            e.Property(x => x.SreqNumber).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.SreqCaseType).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.SreqDescription).HasMaxLength(500);
            e.Property(x => x.SreqStatus).HasMaxLength(25).IsUnicode(false).HasDefaultValue("Draft");
            e.Property(x => x.SreqPriority).HasMaxLength(10).IsUnicode(false).HasDefaultValue("Medium");
            e.Property(x => x.SreqAdvisorDecision).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.SreqAdvisorComment).HasMaxLength(500);
            e.Property(x => x.SreqHodDecision).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.SreqHodComment).HasMaxLength(500);
            e.Property(x => x.SreqCoordinatorComment).HasMaxLength(500);
            e.HasIndex(x => x.SreqNumber).IsUnique().HasDatabaseName("UQ_sreq_number");
            e.HasIndex(x => x.StudentId).HasDatabaseName("IX_sreq_student");
            e.HasIndex(x => new { x.SreqStatus, x.SreqPriority }).HasDatabaseName("IX_sreq_status");
            e.HasOne(x => x.Student).WithMany(s => s.SpecialRequests).HasForeignKey(x => x.StudentId)
                .HasConstraintName("FK_sreq_student").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId)
                .HasConstraintName("FK_sreq_course").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Advisor).WithMany(a => a.SpecialRequests).HasForeignKey(x => x.AdvisorId)
                .HasConstraintName("FK_sreq_advisor").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Hod).WithMany().HasForeignKey(x => x.HodId)
                .HasConstraintName("FK_sreq_hod").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Coordinator).WithMany().HasForeignKey(x => x.CoordId)
                .HasConstraintName("FK_sreq_coord").OnDelete(DeleteBehavior.NoAction);
            // IsRequest::history(): hasMany(RequestStatusHistory, 'sreq_id', 'sreq_id')
            e.HasMany(x => x.History).WithOne(h => h.SpecialRequest).HasForeignKey(h => h.SreqId)
                .HasConstraintName("FK_hist_sreq").OnDelete(DeleteBehavior.NoAction);
        });

        // ------------------------------------------------------------ 000013 special_request_documents
        b.Entity<SpecialRequestDocument>(e =>
        {
            e.ToTable("special_request_documents", t =>
            {
                t.HasCheckConstraint("CK_doc_type", "doc_file_type IN ('pdf','jpg','jpeg','png')");
                t.HasCheckConstraint("CK_doc_size", "doc_file_size BETWEEN 1 AND 5242880");
            });
            e.HasKey(x => x.DocId).HasName("PK_special_request_documents");
            e.Property(x => x.DocFileName).HasMaxLength(150);
            e.Property(x => x.DocFilePath).HasMaxLength(500);
            e.Property(x => x.DocFileType).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.DocUploadDate).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.SreqId).HasDatabaseName("IX_doc_sreq");
            e.HasOne(x => x.Request).WithMany(r => r.Documents).HasForeignKey(x => x.SreqId)
                .HasConstraintName("FK_doc_sreq").OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------ 000014 request_status_history
        b.Entity<RequestStatusHistory>(e =>
        {
            e.ToTable("request_status_history", t =>
            {
                t.HasCheckConstraint("CK_hist_type", "(hist_request_type = 'Normal' AND nreq_id IS NOT NULL AND sreq_id IS NULL) OR (hist_request_type = 'Special' AND sreq_id IS NOT NULL AND nreq_id IS NULL)");
                t.HasCheckConstraint("CK_hist_role", "hist_changed_by_role IN ('Student','Academic Advisor','Head of Department','Scheduling Coordinator','Administrator','System')");
            });
            e.HasKey(x => x.HistId).HasName("PK_request_status_history");
            e.Property(x => x.HistRequestType).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.HistOldStatus).HasMaxLength(25).IsUnicode(false);
            e.Property(x => x.HistNewStatus).HasMaxLength(25).IsUnicode(false);
            e.Property(x => x.HistChangeDate).HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.HistChangedByRole).HasMaxLength(25).IsUnicode(false);
            e.Property(x => x.HistNote).HasMaxLength(300);
            e.HasIndex(x => new { x.NreqId, x.HistChangeDate }).HasDatabaseName("IX_hist_nreq");
            e.HasIndex(x => new { x.SreqId, x.HistChangeDate }).HasDatabaseName("IX_hist_sreq");
        });

        // ------------------------------------------------------------ 000015 queue_tickets
        b.Entity<QueueTicket>(e =>
        {
            e.ToTable("queue_tickets", t =>
            {
                t.HasCheckConstraint("CK_ticket_status", "ticket_status IN ('Waiting','Called','Served','Cancelled','No Show')");
                t.HasCheckConstraint("CK_ticket_times", "(ticket_call_time IS NULL OR ticket_call_time >= ticket_join_time) AND (ticket_end_time IS NULL OR ticket_end_time >= ticket_join_time)");
            });
            e.HasKey(x => x.TicketId).HasName("PK_queue_tickets");
            e.Property(x => x.TicketNumber).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.TicketReason).HasMaxLength(200);
            e.Property(x => x.TicketJoinTime).HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.TicketStatus).HasMaxLength(12).IsUnicode(false).HasDefaultValue("Waiting");
            e.HasIndex(x => x.TicketNumber).IsUnique().HasDatabaseName("UQ_ticket_number");
            // UX_ticket_live: one live ticket per student
            e.HasIndex(x => x.StudentId).IsUnique().HasDatabaseName("UX_ticket_live")
                .HasFilter("[ticket_status] IN ('Waiting','Called')");
            e.HasOne(x => x.Student).WithMany(s => s.QueueTickets).HasForeignKey(x => x.StudentId)
                .HasConstraintName("FK_ticket_student").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Coordinator).WithMany().HasForeignKey(x => x.CoordId)
                .HasConstraintName("FK_ticket_coord").OnDelete(DeleteBehavior.NoAction);
        });
    }
}
