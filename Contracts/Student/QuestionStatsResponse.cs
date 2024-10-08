using Contracts.Diagnostic;
using Contracts.Treatment;

namespace Contracts.Student;

public record QuestionStatsResponse(
    string QuestionId,
    string QuesVersion,
    string QuestionName,
    List<StudentExaminationResponse> Examinations,
    List<StudentProblemResponse> Problems,
    List<TreatmentResponse> Treatments,
    List<DiagnosticResponse> Diagnostics,
    double Problem1_Score,
    double Problem2_Score,
    double Examination_Score,
    double Treatment_Score,
    double DiffDiag_Score,
    double TenDiag_Score,
    string? ExtraQues,
    string? ExtraAns,
    DateTime DateTime
);
