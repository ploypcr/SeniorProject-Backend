using Application.Abstractions;
using Application.Student.Commands;
using Domain.Entities;
using MediatR;

namespace Application.Student.CommandHandlers;

public class CreateStudentStatsCommandHandler : IRequestHandler<CreateStudentStatsCommand, StudentStats>
{
    private readonly IStatsRepository _statsRepository;
    private readonly ITreatmentRepository _treatmentRepository;
    private readonly IProblemRepository _problemRepository;
    private readonly IExaminationRepository _examinationRepository;


    private readonly IDiagnosticRepository _diagnosticRepository;
    private readonly IQuestionRepository _questionRepository;

    public CreateStudentStatsCommandHandler(IExaminationRepository examinationRepository,IStatsRepository statsRepository, ITreatmentRepository treatmentRepository, IDiagnosticRepository diagnosticRepository, IQuestionRepository questionRepository, IProblemRepository problemRepository){
        _statsRepository = statsRepository;
        _treatmentRepository = treatmentRepository;
        _diagnosticRepository = diagnosticRepository;
        _questionRepository = questionRepository;
        _problemRepository = problemRepository;
        _examinationRepository = examinationRepository;
    }
    public async Task<StudentStats> Handle(CreateStudentStatsCommand request, CancellationToken cancellationToken)
    {
        var question = await _questionRepository.GetByIdAsync(new QuestionId(new Guid(request.QuestionId)));
        if(question == null){
            throw new ArgumentException("Don't have this question.");
        }

        var studentSelection = StudentStats.Create(
            request.UserId,
            new QuestionId(new Guid(request.QuestionId)));
        //Console.WriteLine(request.Problems);

        double problems1_score = 0;
        double problems2_score = 0;
        double examinations_score = 0;
        double treatment_score = 0;
        double diff_diagnostic_score = 0;
        double ten_diagnostic_score = 0;


        foreach(var p in request.Problems){
            if(await _problemRepository.GetByIdAsync(new ProblemId(new Guid(p.Id))) == null){
                throw new ArgumentException("No problem found.");
            }
            if(question.Problems.Any(qp => qp.ProblemId == new ProblemId(new Guid(p.Id)) && qp.Round == p.Round)){
                if(p.Round == 1){
                    problems1_score++;
                }
                else{
                    problems2_score++;
                }
            }
            studentSelection.AddProblem(
                new ProblemId(new Guid(p.Id)),
                p.Round
            );
        }

        foreach(var e in request.Examinations){
            if(await _examinationRepository.GetByIdAsync(new ExaminationId(new Guid(e.Id))) == null){
                throw new ArgumentException("No examination found.");
            }
            if(question.Examinations.Any(qe => qe.ExaminationId == new ExaminationId(new Guid(e.Id)))){
                examinations_score++;
            }
            studentSelection.AddExamination(
                new ExaminationId(new Guid(e.Id))
            );
        }
        foreach(var d in request.Diagnostics){
            var diagnostic = await _diagnosticRepository.GetByIdAsync(new DiagnosticId(new Guid(d.Id)));
            if(diagnostic == null){
                throw new ArgumentException("Diagnosis not found.");
            }
            if(question.Diagnostics.Any(qd => qd.Id == new DiagnosticId(new Guid(d.Id)))){
                Console.WriteLine(diagnostic.Type);
                if(diagnostic.Type == "tentative"){
                    ten_diagnostic_score++;
                }
                if(diagnostic.Type == "differential"){
                    diff_diagnostic_score++;
                }
                
            }
            studentSelection.AddDiagnostic(
                diagnostic
            );
        }
        foreach(var t in request.Treatments){
            var treatment = await _treatmentRepository.GetByIdAsync(new TreatmentId(new Guid(t.Id)));
            if(treatment == null){
                throw new ArgumentException("Treatment not found.");
            }
            if(question.Treatments.Any(qt => qt.Id == new TreatmentId(new Guid(t.Id)))){
                treatment_score++;
                
            }
            studentSelection.AddTreatment(
                treatment
            );
        }

        problems1_score = (problems1_score/question.Problems.Where(p => p.Round == 1).Count())*12.5*(request.HeartProblem1/5);
        problems2_score = (problems2_score/question.Problems.Where(p => p.Round == 2).Count())*12.5*(request.HeartProblem2/5);
        examinations_score = (examinations_score/question.Examinations.Count())*12.5;
        treatment_score = (treatment_score/question.Treatments.Count())*25;
        diff_diagnostic_score = (diff_diagnostic_score/question.Diagnostics.Where(d => d.Type == "differential").Count())*25;
        ten_diagnostic_score = (ten_diagnostic_score/question.Diagnostics.Where(d => d.Type == "tentative").Count())*25;


        studentSelection.SetScore(
            problems1_score,
            problems2_score,
            examinations_score,
            treatment_score,
            diff_diagnostic_score,//treatment_score,
            ten_diagnostic_score);//diagnostic_score);
            
        await _statsRepository.AddStudentStats(studentSelection);
        return studentSelection;
    }
}