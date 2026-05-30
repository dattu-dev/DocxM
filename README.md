# DocXM

DocXM là ứng dụng ASP.NET Core MVC dùng để quản lý tài liệu học tập theo môn học và chương. Phạm vi hiện tại là Workflow 1: người dùng đăng nhập, tạo môn học, tải tài liệu lên, nhập tên chương cho tài liệu, hệ thống validate file, lưu file gốc, trích xuất nội dung, chia chunk, sinh embedding và lưu metadata.

## Công nghệ sử dụng

- .NET 8
- ASP.NET Core MVC
- Entity Framework Core 8
- SQL Server / LocalDB
- Database-first
- PdfPig để trích xuất nội dung PDF
- OpenXML để trích xuất nội dung DOCX/PPTX

## Cấu trúc solution

```text
DocXM
|-- AIService
|-- BusinessLogic
|-- BusinessObjects
|-- DataAcessLayer
|-- Presentation
|-- database
`-- DocxM.sln
```

## Kiến trúc

Ứng dụng đi theo mô hình MVC kết hợp kiến trúc 3 layer.

```text
Presentation
  -> BusinessLogic
      -> DataAcessLayer
      -> AIService
      -> BusinessObjects
```

Trách nhiệm từng project:

- `Presentation`: ASP.NET Core MVC, chứa controller, view và view model.
- `BusinessLogic`: chứa nghiệp vụ, validate và điều phối workflow.
- `DataAcessLayer`: chứa `AppDbContext` và repository.
- `BusinessObjects`: chứa entity database-first và enum.
- `AIService`: xử lý validate chữ ký file, trích xuất text, chia chunk, sinh embedding và gọi AI tạo câu trả lời. Nếu có Gemini API key thì dùng Gemini thật; nếu chưa có key thì fallback về AI demo local.
- `database`: chứa script tạo/cập nhật database SQL Server.

`AIService` được tách riêng để sau này thay embedding model hoặc vector database thật mà không trộn logic AI vào MVC hoặc DAL.

## Tác nhân và quyền

Hệ thống hiện chỉ có một tác nhân:

- Người dùng đã đăng nhập.

Hệ thống không còn chia quyền quản trị/sinh viên trong phạm vi workflow hiện tại.

Người dùng có thể:

- Tạo môn học.
- Chỉnh sửa môn học.
- Xóa môn học nếu môn học chưa có chương hoặc tài liệu.
- Nhập tên chương khi tải tài liệu lên; hệ thống tự tạo chương nếu chưa tồn tại trong môn học.
- Xóa chương nếu chương chưa có tài liệu.
- Tải lên tài liệu thuộc chương.
- Xem danh sách tài liệu của mình.
- Xem chi tiết tài liệu của mình.
- Tải file gốc của mình.
- Xóa tài liệu của mình.
- Xử lý lại tài liệu của mình.

Quy tắc nghiệp vụ:

- Một người dùng có thể có nhiều môn học.
- Một môn học có thể có nhiều chương, nhưng chương được tạo thông qua thao tác tải tài liệu.
- Một chương có thể có nhiều tài liệu.
- Một tài liệu chỉ thuộc về một chương.
- Chỉ người tạo/upload mới được xem, tải, xóa hoặc xử lý lại tài liệu đó.

## Workflow xử lý tài liệu

Workflow diễn ra sau khi người dùng tải tài liệu lên hệ thống:

```text
User Upload Document
        |
        v
Document Controller
        |
        v
Document Service
        |
        v
Validate File
        |
        v
Save Original File
        |
        v
Extract Text
        |
        v
Chunk Text
        |
        v
Generate Embedding
        |
        v
Save Metadata to SQL Server
        |
        v
Save Vector to Vector Database
```

Trong phiên bản hiện tại:

- SQL Server lưu metadata tài liệu và chunk.
- `DocumentChunks.VectorId` lưu mã vector tương ứng với mỗi chunk.
- `DocumentChunks.SectionTitle` lưu heading/chapter gần nhất của chunk để chatbot tìm đúng ngữ cảnh hơn.
- Vector database đang được mô phỏng bằng local JSON store tại `Presentation/App_Data/vector-store/vectors.json`.
- Khi có vector database thật, chỉ cần thay implementation của `IVectorStore`.

## Workflow chatbot hỏi đáp tài liệu

Workflow diễn ra khi người dùng mở trang `Chatbot`:

```text
User nhập câu hỏi
        |
        v
Chat Controller
        |
        v
Chat Service
        |
        v
Kiểm tra phạm vi môn học / chương / tài liệu
        |
        v
Tạo embedding cho câu hỏi
        |
        v
Search Vector Database
        |
        v
Retrieve Relevant Chunks
        |
        v
Tạo câu trả lời từ context
        |
        v
Gắn nguồn trích dẫn
        |
        v
Lưu lịch sử hội thoại vào SQL Server
        |
        v
Hiển thị câu trả lời cho người dùng
```

Trong phiên bản hiện tại:

- Người dùng chọn môn học, có thể chọn thêm chương hoặc tài liệu cụ thể.
- Chatbot chỉ tìm trên tài liệu đã xử lý và thuộc tài khoản hiện tại.
- Chatbot có intent detection: câu hỏi hỏi tên dự án/tên tài liệu/tên file/header sẽ trả lời từ metadata tài liệu; câu hỏi hỏi nội dung mới đi qua RAG vector/hybrid search.
- Hệ thống chỉ lấy tối đa 5 chunk liên quan nhất, sau đó build prompt `QUESTION + CONTEXT` và gọi `AIService` để tạo câu trả lời.
- Bước retrieve dùng hybrid search: vector search lấy ứng viên, keyword search ưu tiên từ khóa rõ ràng như `Chapter 2`, `Godot`, `Unity`, `GDScript`, `C#`, sau đó rerank để chọn top chunk tốt nhất.
- Chatbot không trả nguyên chunk; câu trả lời được rút gọn theo trọng tâm câu hỏi và kèm nguồn.
- Câu trả lời tối đa 2 câu; nguồn trích dẫn được hiển thị riêng để tránh lặp trong phần trả lời.
- Trang Chatbot có nút `New chat` và sidebar lịch sử hội thoại.
- Chat hiện tại được giữ bằng session; khi người dùng logout hoặc đóng browser, lần sau vào Chatbot sẽ bắt đầu chat mới.
- Nguồn trích dẫn hiển thị theo tên file, trang nếu có và đoạn nội dung liên quan, tối đa 3 nguồn.
- Lịch sử chat được lưu vào các bảng `ChatConversations`, `ChatMessages`, `ChatCitations`.
- Nếu cấu hình `Gemini:Enabled=true` và có `Gemini:ApiKey`, hệ thống dùng Gemini API thật cho embedding và tạo câu trả lời.
- Nếu chưa cấu hình Gemini API key, hệ thống dùng `PromptAnswerGenerationService` và `DeterministicEmbeddingService` để demo offline.
- Console app có log `[RAG DEBUG]` hiển thị câu hỏi, chunk được retrieve, score từng chunk và prompt cuối cùng gửi vào AI.

## Chức năng hiện có

- Đăng ký, đăng nhập, đăng xuất.
- Quản lý môn học theo từng người dùng.
- Tạo chương tự động theo tên chương người dùng nhập khi tải tài liệu.
- Upload tài liệu PDF, DOCX, PPTX.
- Validate định dạng file, dung lượng, tên file, content type và chữ ký file thật.
- Lưu file gốc theo cấu trúc:

```text
Presentation/wwwroot/uploads/{userId}/{subjectId}/{chapterId}/{file}
```

- Trích xuất nội dung từ PDF/DOCX/PPTX.
- Chia nội dung thành các chunk nhỏ khoảng 400 từ/chunk, overlap 70 từ để giữ ngữ cảnh giữa hai chunk.
- Sinh embedding cho từng chunk. Khi có Gemini key sẽ dùng `gemini-embedding-001`; khi không có key sẽ dùng embedding demo local.
- Lưu vector demo vào local vector store.
- Lưu metadata tài liệu và chunk vào SQL Server.
- Xem danh sách tài liệu theo môn học/chương.
- Xem chi tiết tài liệu và nội dung đã chia chunk.
- Tải file gốc.
- Xóa tài liệu và dữ liệu liên quan.
- Xử lý lại tài liệu.
- Chatbot hỏi đáp theo môn học/chương/tài liệu.
- Hiển thị nguồn trích dẫn cho câu trả lời.
- Lưu và xem lịch sử hội thoại.

## Thiết lập database

Server:

```text
(localdb)\MSSQLLocalDB
```

Database:

```text
DocXM
```

Authentication:

```text
SQL Server Authentication
```

Tài khoản:

```text
User: sa
Password: 123456
```

Connection string trong `Presentation/appsettings.json`:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=DocXM;User Id=sa;Password=123456;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Tạo hoặc cập nhật database:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -U sa -P 123456 -i database\DocXM.sql
```

Nếu database đã có sẵn và chỉ cần thêm bảng cho Chatbot, chạy hoặc copy file:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -U sa -P 123456 -i database\AddChatbotTables.sql
```

Nếu copy script vào SQL Server Management Studio, hãy copy từ file `database/DocXM.sql` hiện tại. File này đã được lưu dạng UTF-8 không BOM để tránh lỗi `Incorrect syntax near '﻿'`.

Nếu database đang bị tạo dở hoặc chưa cần giữ dữ liệu cũ, chạy script reset sạch:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -U sa -P 123456 -i database\ResetDocXM.sql
```

Script reset sẽ xóa database `DocXM` hiện tại rồi tạo lại toàn bộ bảng theo schema mới.

Các bảng chính:

- `AppUsers`
- `Subjects`
- `Chapters`
- `Documents`
- `DocumentChunks`
- `EmbeddingModels`
- `DocumentEmbeddings`
- `ChatConversations`
- `ChatMessages`
- `ChatCitations`

## Chạy ứng dụng

Restore package:

```powershell
dotnet restore DocxM.sln
```

Build solution:

```powershell
dotnet build DocxM.sln
```

Chạy project MVC:

```powershell
dotnet run --project Presentation\Presentation.csproj
```

Route mặc định:

```text
/Subject/Index
```

## Cấu hình Gemini API

Không commit API key vào source code. Nên cấu hình bằng biến môi trường hoặc user secrets.

Trong `Presentation/appsettings.json` có sẵn cấu hình:

```json
"Gemini": {
  "Enabled": false,
  "ApiKey": "",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
  "ChatModel": "gemini-2.5-flash",
  "EmbeddingModel": "gemini-embedding-001",
  "MaxOutputTokens": 512,
  "Temperature": 0.1
}
```

Bật Gemini bằng PowerShell:

```powershell
$env:Gemini__Enabled="true"
$env:Gemini__ApiKey="YOUR_GEMINI_API_KEY"
dotnet run --project Presentation\Presentation.csproj
```

Khi đổi từ embedding demo sang Gemini embedding, nên xử lý lại tài liệu hoặc upload lại tài liệu để vector cũ được tạo lại đúng model embedding.

## Quy tắc validate

Tầng `Presentation`:

- Bắt buộc đăng nhập.
- Bắt buộc chọn môn học.
- Bắt buộc nhập tên chương.
- Bắt buộc nhập tiêu đề tài liệu.
- Bắt buộc chọn file.
- Kiểm tra extension, dung lượng, tên file và content type.

Tầng `BusinessLogic`:

- Người dùng phải tồn tại và đang active.
- Môn học phải thuộc người dùng hiện tại.
- Tên chương phải từ 2 đến 250 ký tự.
- Nếu tên chương đã tồn tại trong môn học đã chọn, hệ thống dùng lại chương đó.
- Nếu tên chương chưa tồn tại, hệ thống tự tạo chương mới trong môn học đã chọn.
- Tiêu đề và mô tả không được vượt quá độ dài quy định.
- File stream phải đọc được.
- File không được rỗng.
- File tối đa 100 MB.
- Chỉ cho phép `.pdf`, `.docx`, `.pptx`.

Tầng `AIService`:

- PDF phải có chữ ký `%PDF-` và mở được bằng PdfPig.
- DOCX/PPTX phải có chữ ký ZIP/OpenXML hợp lệ và mở được bằng OpenXML.
- Text sau khi trích xuất được chia thành chunk để phục vụ tìm kiếm ngữ nghĩa.
- Mỗi chunk được sinh embedding demo và lưu vector.

Nếu validate thất bại sau khi file vật lý đã được ghi, hệ thống sẽ xóa file đó để tránh sinh file rác.

## Database-first

Dự án dùng database-first. Nếu schema SQL thay đổi, scaffold lại entity/context bằng lệnh:

```powershell
dotnet ef dbcontext scaffold "Server=(localdb)\MSSQLLocalDB;Database=DocXM;User Id=sa;Password=123456;TrustServerCertificate=True;MultipleActiveResultSets=True" Microsoft.EntityFrameworkCore.SqlServer --project DataAcessLayer\DataAcessLayer.csproj --startup-project Presentation\Presentation.csproj --context AppDbContext --context-dir . --output-dir ..\BusinessObjects\Entities --namespace BusinessObjects.Entities --context-namespace DataAcessLayer --no-onconfiguring --force
```

Sau khi scaffold, cần kiểm tra lại các partial class đang bổ sung ownership như `Document.Auth.cs`, `Subject.Auth.cs` và `DocumentChunk.Vector.cs`.

## Lưu ý

- Project hiện tên là `DataAcessLayer`. Tên này được giữ nguyên để tránh làm hỏng reference hiện có.
- Controller không gọi trực tiếp `DbContext`; controller chỉ gọi service trong `BusinessLogic`.
- Vector store hiện là bản demo local JSON, chưa phải vector database sản xuất.
- Các thư mục `bin`, `obj` và project MVC cũ đã được dọn khỏi workspace; chúng có thể được tạo lại khi build.
