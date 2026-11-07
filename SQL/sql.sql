
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_ID('jicrss_db') IS NULL CREATE DATABASE jicrss_db;
GO
USE jicrss_db;
GO

/* 1. ADMINISTRATOR (college-wide, no department) ------------------------ */
CREATE TABLE administrators (
    admin_id        INT IDENTITY(1,1) NOT NULL,
    admin_staff_id  VARCHAR(12)   NOT NULL,
    admin_fname     NVARCHAR(50)  NOT NULL,
    admin_lname     NVARCHAR(50)  NOT NULL,
    admin_email     NVARCHAR(120) NOT NULL,
    admin_password  VARCHAR(255)  NOT NULL,
    admin_phone     VARCHAR(12)   NOT NULL,
    admin_is_active BIT           NOT NULL CONSTRAINT DF_admin_active DEFAULT (1),
    CONSTRAINT PK_administrators PRIMARY KEY (admin_id),
    CONSTRAINT UQ_admin_staff UNIQUE (admin_staff_id),
    CONSTRAINT UQ_admin_email UNIQUE (admin_email),
    CONSTRAINT CK_admin_email CHECK (admin_email LIKE '%_@jic.edu.sa'),
    CONSTRAINT CK_admin_phone CHECK (admin_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
                                  OR admin_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO

/* 2. DEPARTMENT ---------------------------------------------------------- */
CREATE TABLE departments (
    dept_id    INT IDENTITY(1,1) NOT NULL,
    dept_code  VARCHAR(10)   NOT NULL,
    dept_name  NVARCHAR(100) NOT NULL,
    dept_email NVARCHAR(120) NOT NULL,
    dept_phone VARCHAR(12)   NOT NULL,
    admin_id   INT           NOT NULL,
    CONSTRAINT PK_departments PRIMARY KEY (dept_id),
    CONSTRAINT UQ_dept_code UNIQUE (dept_code),
    CONSTRAINT UQ_dept_name UNIQUE (dept_name),
    CONSTRAINT FK_dept_admin FOREIGN KEY (admin_id) REFERENCES administrators (admin_id),
    CONSTRAINT CK_dept_email CHECK (dept_email LIKE '%_@jic.edu.sa')
);
GO

/* 3. STUDENT ------------------------------------------------------------- */
CREATE TABLE students (
    student_id              INT IDENTITY(1,1) NOT NULL,
    student_university_id   CHAR(9)       NOT NULL,
    student_fname           NVARCHAR(50)  NOT NULL,
    student_lname           NVARCHAR(50)  NOT NULL,
    student_email           NVARCHAR(120) NOT NULL,
    student_password        VARCHAR(255)  NOT NULL,
    student_phone           VARCHAR(12)   NOT NULL,
    student_academic_level  TINYINT       NOT NULL,
    student_is_active       BIT           NOT NULL CONSTRAINT DF_student_active DEFAULT (1),
    dept_id                 INT           NOT NULL,
    student_gpa             DECIMAL(3,2)  NULL,   -- [UI] "GPA 3.62 / 4.00" on dashboard, forms, advisor review
    student_completed_hours SMALLINT      NULL,   -- [UI] "Completed hours 112", progress to graduation
    CONSTRAINT PK_students PRIMARY KEY (student_id),
    CONSTRAINT UQ_student_univ  UNIQUE (student_university_id),
    CONSTRAINT UQ_student_email UNIQUE (student_email),
    CONSTRAINT UQ_student_phone UNIQUE (student_phone),
    CONSTRAINT FK_student_dept FOREIGN KEY (dept_id) REFERENCES departments (dept_id),
    CONSTRAINT CK_student_univ  CHECK (student_university_id NOT LIKE '%[^0-9]%'),
    CONSTRAINT CK_student_email CHECK (student_email LIKE '%_@jic.edu.sa'),
    CONSTRAINT CK_student_phone CHECK (student_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
                                    OR student_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_student_level CHECK (student_academic_level BETWEEN 1 AND 10),
    CONSTRAINT CK_student_gpa   CHECK (student_gpa IS NULL OR student_gpa BETWEEN 0 AND 4),
    CONSTRAINT CK_student_hours CHECK (student_completed_hours IS NULL OR student_completed_hours BETWEEN 0 AND 200)
);
GO
CREATE INDEX IX_students_dept ON students (dept_id);
GO

/* 4. ACADEMIC_ADVISOR ---------------------------------------------------- */
CREATE TABLE academic_advisors (
    advisor_id        INT IDENTITY(1,1) NOT NULL,
    advisor_staff_id  VARCHAR(12)   NOT NULL,
    advisor_fname     NVARCHAR(50)  NOT NULL,
    advisor_lname     NVARCHAR(50)  NOT NULL,
    advisor_email     NVARCHAR(120) NOT NULL,
    advisor_password  VARCHAR(255)  NOT NULL,
    advisor_phone     VARCHAR(12)   NOT NULL,
    advisor_is_active BIT           NOT NULL CONSTRAINT DF_advisor_active DEFAULT (1),
    dept_id           INT           NOT NULL,
    CONSTRAINT PK_academic_advisors PRIMARY KEY (advisor_id),
    CONSTRAINT UQ_advisor_staff UNIQUE (advisor_staff_id),
    CONSTRAINT UQ_advisor_email UNIQUE (advisor_email),
    CONSTRAINT FK_advisor_dept FOREIGN KEY (dept_id) REFERENCES departments (dept_id),
    CONSTRAINT CK_advisor_email CHECK (advisor_email LIKE '%_@jic.edu.sa'),
    CONSTRAINT CK_advisor_phone CHECK (advisor_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
                                    OR advisor_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO

/* 5. HEAD_OF_DEPARTMENT (exactly one per department) ---------------------- */
CREATE TABLE heads_of_department (
    hod_id        INT IDENTITY(1,1) NOT NULL,
    hod_staff_id  VARCHAR(12)   NOT NULL,
    hod_fname     NVARCHAR(50)  NOT NULL,
    hod_lname     NVARCHAR(50)  NOT NULL,
    hod_email     NVARCHAR(120) NOT NULL,
    hod_password  VARCHAR(255)  NOT NULL,
    hod_phone     VARCHAR(12)   NOT NULL,
    hod_is_active BIT           NOT NULL CONSTRAINT DF_hod_active DEFAULT (1),
    dept_id       INT           NOT NULL,
    CONSTRAINT PK_heads_of_department PRIMARY KEY (hod_id),
    CONSTRAINT UQ_hod_staff UNIQUE (hod_staff_id),
    CONSTRAINT UQ_hod_email UNIQUE (hod_email),
    CONSTRAINT UQ_hod_dept  UNIQUE (dept_id),                 -- a department is headed by one HOD
    CONSTRAINT FK_hod_dept FOREIGN KEY (dept_id) REFERENCES departments (dept_id),
    CONSTRAINT CK_hod_email CHECK (hod_email LIKE '%_@jic.edu.sa'),
    CONSTRAINT CK_hod_phone CHECK (hod_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
                                OR hod_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO

/* 6. SCHEDULING_COORDINATOR ---------------------------------------------- */
CREATE TABLE scheduling_coordinators (
    coord_id        INT IDENTITY(1,1) NOT NULL,
    coord_staff_id  VARCHAR(12)   NOT NULL,
    coord_fname     NVARCHAR(50)  NOT NULL,
    coord_lname     NVARCHAR(50)  NOT NULL,
    coord_email     NVARCHAR(120) NOT NULL,
    coord_password  VARCHAR(255)  NOT NULL,
    coord_phone     VARCHAR(12)   NOT NULL,
    coord_is_active BIT           NOT NULL CONSTRAINT DF_coord_active DEFAULT (1),
    dept_id         INT           NOT NULL,
    CONSTRAINT PK_scheduling_coordinators PRIMARY KEY (coord_id),
    CONSTRAINT UQ_coord_staff UNIQUE (coord_staff_id),
    CONSTRAINT UQ_coord_email UNIQUE (coord_email),
    CONSTRAINT FK_coord_dept FOREIGN KEY (dept_id) REFERENCES departments (dept_id),
    CONSTRAINT CK_coord_email CHECK (coord_email LIKE '%_@jic.edu.sa'),
    CONSTRAINT CK_coord_phone CHECK (coord_phone LIKE '05[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
                                  OR coord_phone LIKE '9665[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO

/* 7. COURSE -------------------------------------------------------------- */
CREATE TABLE courses (
    course_id           INT IDENTITY(1,1) NOT NULL,
    course_code         VARCHAR(10)   NOT NULL,
    course_name         NVARCHAR(120) NOT NULL,
    course_credit_hours TINYINT       NOT NULL,
    course_level        TINYINT       NOT NULL,
    course_is_active    BIT           NOT NULL CONSTRAINT DF_course_active DEFAULT (1),
    dept_id             INT           NOT NULL,
    CONSTRAINT PK_courses PRIMARY KEY (course_id),
    CONSTRAINT UQ_course_code UNIQUE (course_code),
    CONSTRAINT FK_course_dept FOREIGN KEY (dept_id) REFERENCES departments (dept_id),
    CONSTRAINT CK_course_hours CHECK (course_credit_hours BETWEEN 0 AND 6),
    CONSTRAINT CK_course_level CHECK (course_level BETWEEN 1 AND 10)
);
GO
CREATE INDEX IX_courses_dept ON courses (dept_id);
GO

/* 8. COURSE_PREREQUISITE (bridge, composite key) -------------------------- */
CREATE TABLE course_prerequisites (
    course_id        INT NOT NULL,
    prereq_course_id INT NOT NULL,
    CONSTRAINT PK_course_prerequisites PRIMARY KEY (course_id, prereq_course_id),
    CONSTRAINT FK_prereq_course FOREIGN KEY (course_id)        REFERENCES courses (course_id),
    CONSTRAINT FK_prereq_prereq FOREIGN KEY (prereq_course_id) REFERENCES courses (course_id),
    CONSTRAINT CK_prereq_not_self CHECK (course_id <> prereq_course_id)
);
GO

/* 9. SECTION ------------------------------------------------------------- */
CREATE TABLE sections (
    section_id         INT IDENTITY(1,1) NOT NULL,
    section_number     VARCHAR(5)    NOT NULL,
    section_semester   VARCHAR(5)    NOT NULL,     -- e.g. '481'
    section_days       VARCHAR(20)   NOT NULL,     -- e.g. 'Mon/Wed'
    section_start_time TIME(0)       NOT NULL,
    section_end_time   TIME(0)       NOT NULL,
    section_room       VARCHAR(15)   NOT NULL,     -- e.g. 'B51-118'
    section_instructor NVARCHAR(100) NOT NULL,
    section_capacity   SMALLINT      NOT NULL,
    section_enrolled   SMALLINT      NOT NULL CONSTRAINT DF_section_enrolled DEFAULT (0),
    section_status     VARCHAR(10)   NOT NULL CONSTRAINT DF_section_status DEFAULT ('Open'),
    course_id          INT           NOT NULL,
    CONSTRAINT PK_sections PRIMARY KEY (section_id),
    CONSTRAINT UQ_section UNIQUE (course_id, section_semester, section_number),
    CONSTRAINT FK_section_course FOREIGN KEY (course_id) REFERENCES courses (course_id),
    CONSTRAINT CK_section_time   CHECK (section_end_time > section_start_time),
    CONSTRAINT CK_section_seats  CHECK (section_capacity > 0 AND section_enrolled BETWEEN 0 AND section_capacity),
    CONSTRAINT CK_section_status CHECK (section_status IN ('Open','Full','Closed','Cancelled'))
);
GO

/* 10. ENROLLMENT (the student schedule) ----------------------------------- */
CREATE TABLE enrollments (
    enroll_id              INT IDENTITY(1,1) NOT NULL,
    enroll_semester        VARCHAR(5)   NOT NULL,
    enroll_status          VARCHAR(12)  NOT NULL CONSTRAINT DF_enroll_status DEFAULT ('Registered'),
    enroll_registered_date DATETIME2(0) NOT NULL CONSTRAINT DF_enroll_reg DEFAULT (SYSDATETIME()),
    enroll_updated_date    DATETIME2(0) NULL,
    student_id             INT          NOT NULL,
    section_id             INT          NOT NULL,
    coord_id               INT          NULL,      -- set when a coordinator applied an accepted request
    CONSTRAINT PK_enrollments PRIMARY KEY (enroll_id),
    CONSTRAINT FK_enroll_student FOREIGN KEY (student_id) REFERENCES students (student_id),
    CONSTRAINT FK_enroll_section FOREIGN KEY (section_id) REFERENCES sections (section_id),
    CONSTRAINT FK_enroll_coord   FOREIGN KEY (coord_id)   REFERENCES scheduling_coordinators (coord_id),
    CONSTRAINT CK_enroll_status CHECK (enroll_status IN ('Registered','Dropped'))
);
GO
CREATE UNIQUE INDEX UX_enroll_active ON enrollments (student_id, section_id) WHERE enroll_status = 'Registered';
CREATE INDEX IX_enroll_section ON enrollments (section_id);
GO

/* 11. NORMAL_REQUEST — handled by the coordinator only -------------------- */
CREATE TABLE normal_requests (
    nreq_id                  INT IDENTITY(1,1) NOT NULL,
    nreq_number              VARCHAR(10)    NOT NULL,     -- 'NR-1187'
    nreq_description         NVARCHAR(500)  NOT NULL,
    nreq_status              VARCHAR(15)    NOT NULL CONSTRAINT DF_nreq_status DEFAULT ('Draft'),
    nreq_priority            VARCHAR(10)    NOT NULL CONSTRAINT DF_nreq_priority DEFAULT ('Medium'),
    nreq_submitted_date      DATETIME2(0)   NULL,         -- NULL while Draft
    nreq_coordinator_comment NVARCHAR(500)  NULL,
    nreq_closed_date         DATETIME2(0)   NULL,
    student_id               INT            NOT NULL,
    course_id                INT            NOT NULL,
    current_section_id       INT            NULL,         -- empty for "Add course"
    requested_section_id     INT            NULL,
    coord_id                 INT            NULL,
    nreq_reason              VARCHAR(40)    NOT NULL,     -- [UI] UC1-03 "Reason *" drop-down
    CONSTRAINT PK_normal_requests PRIMARY KEY (nreq_id),
    CONSTRAINT UQ_nreq_number UNIQUE (nreq_number),
    CONSTRAINT FK_nreq_student   FOREIGN KEY (student_id)           REFERENCES students (student_id),
    CONSTRAINT FK_nreq_course    FOREIGN KEY (course_id)            REFERENCES courses (course_id),
    CONSTRAINT FK_nreq_current   FOREIGN KEY (current_section_id)   REFERENCES sections (section_id),
    CONSTRAINT FK_nreq_requested FOREIGN KEY (requested_section_id) REFERENCES sections (section_id),
    CONSTRAINT FK_nreq_coord     FOREIGN KEY (coord_id)             REFERENCES scheduling_coordinators (coord_id),
    CONSTRAINT CK_nreq_status   CHECK (nreq_status IN ('Draft','New','Under Process','Completed','Rejected','Cancelled')),
    CONSTRAINT CK_nreq_priority CHECK (nreq_priority IN ('High','Medium','Low')),
    CONSTRAINT CK_nreq_reason   CHECK (nreq_reason IN ('Section is full','Time conflict','Course missing from my schedule','Missed my registration day','Other')),
    CONSTRAINT CK_nreq_desc     CHECK (LEN(nreq_description) >= 30),
    CONSTRAINT CK_nreq_sections CHECK (current_section_id IS NULL OR requested_section_id IS NULL OR current_section_id <> requested_section_id),
    CONSTRAINT CK_nreq_submitted CHECK (nreq_status = 'Draft' OR nreq_submitted_date IS NOT NULL),
    CONSTRAINT CK_nreq_closed    CHECK (nreq_status NOT IN ('Completed','Rejected','Cancelled') OR nreq_closed_date IS NOT NULL)
);
GO
CREATE INDEX IX_nreq_student ON normal_requests (student_id);
CREATE INDEX IX_nreq_status  ON normal_requests (nreq_status, nreq_priority);
GO

/* 12. SPECIAL_REQUEST — advisor -> head of department -> coordinator ----- */
CREATE TABLE special_requests (
    sreq_id                    INT IDENTITY(1,1) NOT NULL,
    sreq_number                VARCHAR(10)    NOT NULL,   -- 'SR-2451'
    sreq_case_type             VARCHAR(40)    NOT NULL,
    sreq_description           NVARCHAR(500)  NOT NULL,
    sreq_status                VARCHAR(25)    NOT NULL CONSTRAINT DF_sreq_status DEFAULT ('Draft'),
    sreq_priority              VARCHAR(10)    NOT NULL CONSTRAINT DF_sreq_priority DEFAULT ('Medium'),
    sreq_submitted_date        DATETIME2(0)   NULL,
    sreq_advisor_decision      VARCHAR(10)    NULL,
    sreq_advisor_comment       NVARCHAR(500)  NULL,
    sreq_advisor_decision_date DATETIME2(0)   NULL,
    sreq_hod_decision          VARCHAR(10)    NULL,
    sreq_hod_comment           NVARCHAR(500)  NULL,
    sreq_hod_decision_date     DATETIME2(0)   NULL,
    sreq_coordinator_comment   NVARCHAR(500)  NULL,
    sreq_closed_date           DATETIME2(0)   NULL,
    student_id                 INT            NOT NULL,
    course_id                  INT            NOT NULL,
    advisor_id                 INT            NULL,
    hod_id                     INT            NULL,
    coord_id                   INT            NULL,
    CONSTRAINT PK_special_requests PRIMARY KEY (sreq_id),
    CONSTRAINT UQ_sreq_number UNIQUE (sreq_number),
    CONSTRAINT FK_sreq_student FOREIGN KEY (student_id) REFERENCES students (student_id),
    CONSTRAINT FK_sreq_course  FOREIGN KEY (course_id)  REFERENCES courses (course_id),
    CONSTRAINT FK_sreq_advisor FOREIGN KEY (advisor_id) REFERENCES academic_advisors (advisor_id),
    CONSTRAINT FK_sreq_hod     FOREIGN KEY (hod_id)     REFERENCES heads_of_department (hod_id),
    CONSTRAINT FK_sreq_coord   FOREIGN KEY (coord_id)   REFERENCES scheduling_coordinators (coord_id),
    CONSTRAINT CK_sreq_case     CHECK (sreq_case_type IN ('Prerequisite Exemption Form','Course Substitution','Credit Overload','Graduation Exception','Late Withdrawal')),
    CONSTRAINT CK_sreq_status   CHECK (sreq_status IN ('Draft','With Advisor','With Head of Department','Accepted','Completed','Rejected','Cancelled')),
    CONSTRAINT CK_sreq_priority CHECK (sreq_priority IN ('High','Medium','Low')),
    CONSTRAINT CK_sreq_adv_dec  CHECK (sreq_advisor_decision IS NULL OR sreq_advisor_decision IN ('Accepted','Rejected')),
    CONSTRAINT CK_sreq_hod_dec  CHECK (sreq_hod_decision IS NULL OR sreq_hod_decision IN ('Accepted','Rejected')),
    CONSTRAINT CK_sreq_desc     CHECK (LEN(sreq_description) >= 30),
    -- a decision always comes with its date and the deciding person
    CONSTRAINT CK_sreq_adv_trio CHECK ((sreq_advisor_decision IS NULL AND sreq_advisor_decision_date IS NULL)
                                    OR (sreq_advisor_decision IS NOT NULL AND sreq_advisor_decision_date IS NOT NULL AND advisor_id IS NOT NULL)),
    CONSTRAINT CK_sreq_hod_trio CHECK ((sreq_hod_decision IS NULL AND sreq_hod_decision_date IS NULL)
                                    OR (sreq_hod_decision IS NOT NULL AND sreq_hod_decision_date IS NOT NULL AND hod_id IS NOT NULL)),
    -- the head of department decides only after the advisor accepted
    CONSTRAINT CK_sreq_order    CHECK (sreq_hod_decision IS NULL OR sreq_advisor_decision = 'Accepted'),
    -- a rejection needs a written reason of at least 20 characters (UC2-06)
    CONSTRAINT CK_sreq_adv_why  CHECK (sreq_advisor_decision IS NULL OR sreq_advisor_decision = 'Accepted' OR LEN(sreq_advisor_comment) >= 20),
    CONSTRAINT CK_sreq_hod_why  CHECK (sreq_hod_decision IS NULL OR sreq_hod_decision = 'Accepted' OR LEN(sreq_hod_comment) >= 20),
    CONSTRAINT CK_sreq_submitted CHECK (sreq_status = 'Draft' OR sreq_submitted_date IS NOT NULL)
);
GO
CREATE INDEX IX_sreq_student ON special_requests (student_id);
CREATE INDEX IX_sreq_status  ON special_requests (sreq_status, sreq_priority);
GO

/* 13. SPECIAL_REQUEST_DOCUMENT (max 5 MB — NFR) --------------------------- */
CREATE TABLE special_request_documents (
    doc_id          INT IDENTITY(1,1) NOT NULL,
    doc_file_name   NVARCHAR(150) NOT NULL,
    doc_file_path   NVARCHAR(500) NOT NULL,
    doc_file_type   VARCHAR(10)   NOT NULL,
    doc_file_size   INT           NOT NULL,      -- bytes
    doc_upload_date DATETIME2(0)  NOT NULL CONSTRAINT DF_doc_upload DEFAULT (SYSDATETIME()),
    sreq_id         INT           NOT NULL,
    CONSTRAINT PK_special_request_documents PRIMARY KEY (doc_id),
    CONSTRAINT FK_doc_sreq FOREIGN KEY (sreq_id) REFERENCES special_requests (sreq_id) ON DELETE CASCADE,
    CONSTRAINT CK_doc_type CHECK (doc_file_type IN ('pdf','jpg','jpeg','png')),
    CONSTRAINT CK_doc_size CHECK (doc_file_size BETWEEN 1 AND 5242880)
);
GO
CREATE INDEX IX_doc_sreq ON special_request_documents (sreq_id);
GO

/* 14. REQUEST_STATUS_HISTORY (audit trail + report source) --------------- */
CREATE TABLE request_status_history (
    hist_id              INT IDENTITY(1,1) NOT NULL,
    hist_request_type    VARCHAR(10)   NOT NULL,
    hist_old_status      VARCHAR(25)   NULL,       -- NULL on the first record (submission)
    hist_new_status      VARCHAR(25)   NOT NULL,
    hist_change_date     DATETIME2(0)  NOT NULL CONSTRAINT DF_hist_date DEFAULT (SYSDATETIME()),
    hist_changed_by_role VARCHAR(25)   NOT NULL,
    hist_note            NVARCHAR(300) NULL,
    nreq_id              INT           NULL,
    sreq_id              INT           NULL,
    CONSTRAINT PK_request_status_history PRIMARY KEY (hist_id),
    CONSTRAINT FK_hist_nreq FOREIGN KEY (nreq_id) REFERENCES normal_requests (nreq_id),
    CONSTRAINT FK_hist_sreq FOREIGN KEY (sreq_id) REFERENCES special_requests (sreq_id),
    CONSTRAINT CK_hist_type CHECK ((hist_request_type = 'Normal'  AND nreq_id IS NOT NULL AND sreq_id IS NULL)
                                OR (hist_request_type = 'Special' AND sreq_id IS NOT NULL AND nreq_id IS NULL)),
    CONSTRAINT CK_hist_role CHECK (hist_changed_by_role IN ('Student','Academic Advisor','Head of Department','Scheduling Coordinator','Administrator','System'))
);
GO
CREATE INDEX IX_hist_nreq ON request_status_history (nreq_id, hist_change_date);
CREATE INDEX IX_hist_sreq ON request_status_history (sreq_id, hist_change_date);
GO

/* 15. QUEUE_TICKET (virtual waiting queue) ------------------------------- */
CREATE TABLE queue_tickets (
    ticket_id        INT IDENTITY(1,1) NOT NULL,
    ticket_number    VARCHAR(10)   NOT NULL,      -- 'Q-481-014'
    ticket_reason    NVARCHAR(200) NOT NULL,
    ticket_join_time DATETIME2(0)  NOT NULL CONSTRAINT DF_ticket_join DEFAULT (SYSDATETIME()),
    ticket_call_time DATETIME2(0)  NULL,
    ticket_end_time  DATETIME2(0)  NULL,
    ticket_status    VARCHAR(12)   NOT NULL CONSTRAINT DF_ticket_status DEFAULT ('Waiting'),
    student_id       INT           NOT NULL,
    coord_id         INT           NULL,
    CONSTRAINT PK_queue_tickets PRIMARY KEY (ticket_id),
    CONSTRAINT UQ_ticket_number UNIQUE (ticket_number),
    CONSTRAINT FK_ticket_student FOREIGN KEY (student_id) REFERENCES students (student_id),
    CONSTRAINT FK_ticket_coord   FOREIGN KEY (coord_id)   REFERENCES scheduling_coordinators (coord_id),
    CONSTRAINT CK_ticket_status CHECK (ticket_status IN ('Waiting','Called','Served','Cancelled','No Show')),
    CONSTRAINT CK_ticket_times  CHECK ((ticket_call_time IS NULL OR ticket_call_time >= ticket_join_time)
                                   AND (ticket_end_time  IS NULL OR ticket_end_time  >= ticket_join_time))
);
GO
-- a student holds at most one live ticket at a time
CREATE UNIQUE INDEX UX_ticket_live ON queue_tickets (student_id) WHERE ticket_status IN ('Waiting','Called');
GO
