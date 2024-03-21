using System.Drawing;
using System.Linq.Expressions;
using Application.Abstractions;
using Application.Abstractions.Services;
using Application.Questions.Commands;
using Domain.Entities;
using ExcelDataReader;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;

namespace Infrastructure.Services;
public class FileStorageService : IFileStorageService{
    private readonly IWebHostEnvironment _environment;
    private readonly IExaminationRepository _examinationRepository;
    private readonly IProblemRepository _problemRepository;
    private readonly ITreatmentRepository _treatmentRepository;
    private readonly IDiagnosticRepository _diagnosticRepository;
    private readonly ITagRepository _tagRepository;

    public FileStorageService(IWebHostEnvironment env,IProblemRepository problemRepository, IExaminationRepository examinationRepository, ITreatmentRepository treatmentRepository, IDiagnosticRepository diagnosticRepository, ITagRepository tagRepository){
        _environment = env;
        _examinationRepository = examinationRepository;
        _problemRepository = problemRepository;
        _tagRepository = tagRepository;
        _treatmentRepository = treatmentRepository;
        _diagnosticRepository = diagnosticRepository;
    }

    public async Task<string> UploadExaminationImg(string examinationId, IFormFile? file, byte[] imageBytes, string oldImg)
    {
        //var contentPath = _environment.ContentRootPath;
        //docker version
        //var contentPath = "app";

        var path = Path.Combine("Uploads", examinationId);
        //var path = Path.Combine("Uploads", examinationId);
        
        if(oldImg != null){
            if(File.Exists(Path.Combine(oldImg))){
                File.Delete(Path.Combine(oldImg));
            }
        }

        if(!Directory.Exists(path)){
            Directory.CreateDirectory(path);
        }
        
        var ext = string.Empty;
        if(file == null){
            ext = ".png";
        }
        ext = Path.GetExtension(ext == string.Empty? file.FileName : ext);

        var fileName = Guid.NewGuid().ToString() + ext;

        var filePath = Path.Combine(path, fileName);
        Console.WriteLine(filePath);
        if(file != null){
            try{
                using (FileStream fs = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(fs);
                }
                // using (FileStream fs = System.IO.File.Create(filePath))
                // {
                //     await file.CopyToAsync(fs);
                // }
            }catch(Exception ex){
                throw new Exception($"Can't upload files : {ex.Message}");
            }
        }
        else{
            //Console.WriteLine("No");

            await File.WriteAllBytesAsync(filePath, imageBytes);
        }
        return Path.Combine(path, fileName);
    }

    public void DeleteFile(string filePath){
        //var contentPath = _environment.ContentRootPath;
        //docker version
        //var contentPath = "app";
        // var path = Path.Combine(filePath);
        // if(File.Exists(path)){
        //     File.Delete(path);
        // }
        //docker
        if(File.Exists(filePath)){
            File.Delete(filePath);
        }
    }

    public void DeleteDirectory(string examinationId){
        var contentPath = _environment.ContentRootPath;
        //var path = Path.Combine(contentPath, "Uploads", examinationId);

        //docker version
        //var contentPath = "app";
        var path = Path.Combine( "Uploads", examinationId);

        if(Directory.Exists(path)){
            foreach(var fi in Directory.GetFiles(path))
            {
                File.Delete(fi);
            }
            Directory.Delete(path);
        }
    }

    public bool CheckIfFilePathExist(string filePath)
    {
        //var contentPath = _environment.ContentRootPath;
        //docker version
        // var contentPath = "app";
        if(!File.Exists(Path.Combine(filePath))){
            return false;
        }
        // if(!File.Exists(filePath)){
        //     return false;
        // }
        return true;
    }

    public async Task<List<CreateQuestionCommand>> ImportExcelFileToDB(IFormFile file, string userId)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using (var package = new ExcelPackage(stream))
            {    
                List<CreateQuestionCommand> questions = new ();
                var worksheet = package.Workbook.Worksheets[0];
                var drawings = worksheet.Drawings;
                int i = 2;
                var emptyRow = false;
                //Console.WriteLine(worksheet.Cells[2,13].Value == null);
                var lookUpDrawing = worksheet.Drawings.ToLookup(x => $"{ x.From.Row}_{x.From.Column}");
                IEnumerable<string> keys = lookUpDrawing.Select(t => t.Key);
                foreach(string k in keys){
                    Console.WriteLine(k);
                }
                Console.WriteLine(worksheet.Dimension.End.Row);
                for(;!emptyRow;){
                    if(worksheet.Cells[i,1].Value == null && worksheet.Cells[i,12].Value == null){
                        emptyRow=true;
                        //Console.WriteLine("y");
                        continue;
                    }
                    var signalment_species = worksheet.Cells[i,2].GetValue<string>();
                    var signalment_breed = worksheet.Cells[i,3].GetValue<string>();
                    var signalment_gender = worksheet.Cells[i,4].GetValue<string>();
                    
                    var signalment_sterilize = worksheet.Cells[i,5].GetValue<string>();
                    var signalment_age = worksheet.Cells[i,6].GetValue<string>();
                    var signalment_weight = worksheet.Cells[i,7].GetValue<string>();
                    SignalmentCommand signalment = new SignalmentCommand(
                        signalment_species,
                        signalment_breed,
                        signalment_gender,
                        signalment_sterilize == "y" ? true : false,
                        signalment_age,
                        signalment_weight
                    );
                    var client_complains = worksheet.Cells[i,8].GetValue<string>();
                    var historytaking_info = worksheet.Cells[i,9].GetValue<string>();
                    var general_info = worksheet.Cells[i,10].GetValue<string>();
                    var problems_1 = worksheet.Cells[i,11].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();

                    List<ProblemCommand> problemCommands = new();
                    foreach(string name in problems_1){
                        var p = await _problemRepository.GetByNameAsync(name);
                        if(p == null){
                            throw new ArgumentException("Problem not found.");
                        }
                        problemCommands.Add(new ProblemCommand(p.Id.Value.ToString(),1));
                    }

                    var diff_diagnostics = worksheet.Cells[i,12].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    //var ten_diagnostics = worksheet.Cells[i,20].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();

                    List<DiagnosticCommand> diagnosticCommands = new();
                    foreach(string name in diff_diagnostics){
                        var d = await _diagnosticRepository.GetByNameAndTypeAsync(name,"Differential");
                        if(d == null){
                            throw new ArgumentException("Diagnosis not found.");
                        }
                        diagnosticCommands.Add(new DiagnosticCommand(d.Id.Value.ToString()));
                    }


                    var examination1_num = worksheet.Cells[i,13].GetValue<int>();
                    List<ExaminationCommand> examinationCommands = new();
                    for(int j = i; j < examination1_num+i;j++){
                        var examination1_lab = worksheet.Cells[j,14].GetValue<string>();
                        var examination1_type = worksheet.Cells[j,15].GetValue<string>();
                        var examination1_name = worksheet.Cells[j,16].GetValue<string>();
                        var examination1_area = worksheet.Cells[j,17].GetValue<string>();
                        var examination1_text = worksheet.Cells[j,18].GetValue<string>();
                        var examination1 = await _examinationRepository.GetByDetails(examination1_name, examination1_type == string.Empty ? null : examination1_type, examination1_lab, examination1_area == string.Empty ? null : examination1_area);
                        if(examination1 == null){
                            throw new ArgumentException("Examination not found");
                        }
                        var lookUpKey = $"{j-1}_{18}";
                        //var examination1_img = string.Empty;
                        if(lookUpDrawing.Contains(lookUpKey))
                        {
                            Console.WriteLine($"******************\n{lookUpKey}\n***********\n");
                            ExcelPicture excel_image = lookUpDrawing[lookUpKey].ToList()[0] as ExcelPicture;
                            var imageBytes = excel_image.Image.ImageBytes;
                            //Console.WriteLine(imageBytes);
                            var examination1_img = await UploadExaminationImg(examination1.Id.Value.ToString(), null, imageBytes, null);
                            examinationCommands.Add(new ExaminationCommand(
                                examination1.Id.Value.ToString(),
                                examination1_text, 
                                examination1_img
                            ));
                        }else{
                            examinationCommands.Add(new ExaminationCommand(
                                examination1.Id.Value.ToString(),
                                examination1_text, 
                                null
                            ));
                        }
                    }

                    var problems_2 = worksheet.Cells[i,20].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    foreach(string name in problems_2){
                        var p = await _problemRepository.GetByNameAsync(name);
                        if(p == null){
                            throw new ArgumentException("No problem found.");
                        }
                        problemCommands.Add(new ProblemCommand(
                            p.Id.Value.ToString(),
                            2
                        ));
                    }

                    var ten_diagnostics = worksheet.Cells[i,21].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    foreach(string name in ten_diagnostics){
                        var d = await _diagnosticRepository.GetByNameAndTypeAsync(name,"Tentative");
                        if(d == null){
                            throw new ArgumentException("No diagnosis found.");
                        }
                        diagnosticCommands.Add(new DiagnosticCommand(d.Id.Value.ToString()));
                    }

                    var treatment_types = worksheet.Cells[i,22].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    var treatment_names = worksheet.Cells[i,23].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    var treatments = treatment_types.Zip(treatment_names, (t,n) => new { Type = t, Name = n});
                    List<TreatmentCommand> treatmentCommands = new();
                    foreach(var t in treatments){
                        var treatment = await _treatmentRepository.GetByNameAsync(t.Name);
                        if(treatment == null){
                            throw new ArgumentException("No treatment found.");
                        }
                        treatmentCommands.Add(new TreatmentCommand(treatment.Id.Value.ToString()));
                    }

                    var tags = worksheet.Cells[i,24].GetValue<string>().Split(",").Select(t => t.Trim()).ToList();
                    List<TagCommand> tagCommands = new();
                    foreach(string t in tags){
                        var tag = await _tagRepository.GetByNameAsync(t);
                        if(tag == null){
                            throw new Exception("No tag found.");
                        }
                        tagCommands.Add(new TagCommand(tag.Id.Value.ToString()));
                    }
                    Console.WriteLine(treatmentCommands[0].Id);
                    questions.Add(new CreateQuestionCommand(
                        client_complains,
                        historytaking_info,
                        general_info,
                        signalment,
                        problemCommands,
                        examinationCommands,
                        treatmentCommands,
                        diagnosticCommands,
                        tagCommands,
                        userId,
                        1
                    ));

                    var next = examination1_num == 0 ? 1 : examination1_num;
                    i += next;

                // }
                }
                return questions;
            }
        }
    }

    public async Task<byte[]> GetExcelTemplate()
    {
        ExcelPackage excel = new ExcelPackage(); 
  
        var workSheet1 = excel.Workbook.Worksheets.Add("Template"); 
        workSheet1.DefaultColWidth = 40;

        var workSheet2 = excel.Workbook.Worksheets.Add("Problem List"); 
        workSheet2.DefaultColWidth = 30;

        var workSheet3 = excel.Workbook.Worksheets.Add("Examination List"); 
        workSheet3.DefaultColWidth = 30;

        var workSheet4 = excel.Workbook.Worksheets.Add("Treatment List"); 
        workSheet4.DefaultColWidth = 30;

        var workSheet5 = excel.Workbook.Worksheets.Add("Diagnostic List"); 
        workSheet5.DefaultColWidth = 30;

        var workSheet6 = excel.Workbook.Worksheets.Add("Tag List"); 
        workSheet6.DefaultColWidth = 30;


        string[] columnName1 = {
                "เลขข้อ", 
                "ประเภท", 
                "สายพันธุ์",
                "เพศ",
                "ทำหมัน (y/n)",
                "อายุ",
                "น้ำหนัก",
                "Client complains",
                "History taking",
                "ผลตรวจทั่วไป (คั่นด้วยเครื่องหมาย , )",
                "Problem List 1 (คั่นด้วยเครื่องหมาย , )",
                "Differential Diagnosis (คั่นด้วยเครื่องหมาย , )",
                "จำนวนการส่งตรวจ 1 (ใส่แค่ตัวเลข)",
                "แผนกการส่งตรวจ 1",
                "หัวข้อการส่งตรวจ 1 (Optional)",
                "ชื่อการส่งตรวจ 1",
                "ตัวอย่างที่นำไปส่งตรวจ 1 (Optional)",
                "ผลการส่งตรวจ 1 (Text)",
                "ผลการส่งตรวจ 1 (รูปภาพ) (ถ้ามี)",
                "Problem List 2 (คั่นด้วย , )",
                "Tentative/Definitive Diagnosis (คั่นด้วยเครื่องหมาย , )",
                "ประเภท Treatment (คั่นด้วยเครื่องหมาย , )",
                "ชื่อ Treatment (คั่นด้วยเครื่องหมาย , )",
                "Tag ที่เกี่ยวข้อง (คั่นด้วยเครื่องหมาย , )"

        };

        string[] columnName2 = {
            "ชื่อ Problem"
        };

        string[] columnName3 = {
            "แผนกการส่งตรวจ",
            "หัวข้อการส่่งตรวจ",
            "ชื่อการส่งตรวจ",
            "ตัวอย่างที่นำไปส่งตรวจ"
        };
        string[] columnName4 = {
            "ประเภท Treatment",
            "ชื่อ Treatment"
        };
        string[] columnName5 = {
            "ประเภท Diagnosis",
            "ชื่อ Diagnosis"
        };
        string[] columnName6 = {
            "ชื่อ Tag"
        };

        for (int i = 0; i < columnName1.Length; i++)
        {
            workSheet1.Cells[1, i+1].Value = columnName1[i];
        }

        for (int i = 0; i < columnName2.Length; i++)
        {
            workSheet2.Cells[1, i+1].Value = columnName2[i];
        }

        for (int i = 0; i < columnName3.Length; i++)
        {
            workSheet3.Cells[1, i+1].Value = columnName3[i];
        }

        for (int i = 0; i < columnName4.Length; i++)
        {
            workSheet4.Cells[1, i+1].Value = columnName4[i];
        }

        for (int i = 0; i < columnName5.Length; i++)
        {
            workSheet5.Cells[1, i+1].Value = columnName5[i];
        }

        for (int i = 0; i < columnName6.Length; i++)
        {
            workSheet6.Cells[1, i+1].Value = columnName6[i];
        }


        //problem
        var problems = await _problemRepository.GetAllProblemsAsync();
        for(int i = 0;i < problems.Count;i++){
            workSheet2.Cells[i+2,1].Value = problems[i].Name;
        }

        var examinations = await _examinationRepository.GetAllExaminationsAsync();
        for(int i = 0;i < examinations.Count;i++){
            workSheet3.Cells[i+2,1].Value = examinations[i].Lab;
            workSheet3.Cells[i+2,2].Value = examinations[i].Type == null ? string.Empty : examinations[i].Type;
            workSheet3.Cells[i+2,3].Value = examinations[i].Name;
            workSheet3.Cells[i+2,4].Value = examinations[i].Area == null ? string.Empty : examinations[i].Area;

        }

        var treatments = await _treatmentRepository.GetAllTreatmentAsync();
        for(int i = 0;i < treatments.Count;i++){
            workSheet4.Cells[i+2,1].Value = treatments[i].Type;
            workSheet4.Cells[i+2,2].Value = treatments[i].Name;
        }
        
        var diagnostics = await _diagnosticRepository.GetAllDiagnosticsAsync();
        for(int i = 0;i < diagnostics.Count;i++){
            workSheet5.Cells[i+2,1].Value = diagnostics[i].Type == "tentative" ?"Tentative":"Differential";
            workSheet5.Cells[i+2,2].Value = diagnostics[i].Name;

        }

        var tags = await _tagRepository.GetAllTagsAsync();
        for(int i = 0;i < tags.Count;i++){
            workSheet6.Cells[i+2,1].Value = tags[i].Name;
        }

        return await excel.GetAsByteArrayAsync();
    }
}