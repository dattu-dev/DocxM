# DocXM

DocXM là hệ thống ASP.NET Core MVC chạy trên .NET 8 để quản lý tài liệu học tập theo môn học. Ứng dụng hỗ trợ giảng viên tải lên tài liệu PDF/DOCX/PPTX, sinh dữ liệu phục vụ RAG, cho phép xem trước PDF trên web, tải file gốc và dùng chatbot hỏi đáp theo phạm vi tài liệu được cấp quyền.

## Chức năng chính

- Quản lý môn học, chương và tài liệu học tập.
- Upload tài liệu PDF, DOCX, PPTX.
- Preview PDF trực tiếp trên web.
- Download file gốc qua controller có kiểm tra quyền.
- Chatbot RAG hỏi đáp theo nội dung tài liệu.
- Phân quyền Instructor/Student theo Subject.
- Cấp/thu hồi quyền xem Subject cho Student bằng email.

## Kiến trúc solution

```text
DocXM
|-- Presentation
|-- BusinessLogic
|-- DataAcessLayer
|-- BusinessObjects
|-- AIService
|-- database
`-- DocxM.sln
```

Các project chính:

- `Presentation`: ASP.NET Core MVC, chứa controller, view, view model, cấu hình app và static assets.
- `BusinessLogic`: nghiệp vụ, validate, điều phối upload/xử lý tài liệu, phân quyền và chatbot.
- `DataAcessLayer`: `AppDbContext` và repository truy vấn SQL Server.
- `BusinessObjects`: entity, enum và hằng số role dùng chung.
- `AIService`: validate file signature, extract text, chunking, embedding, vector store và answer generation.

Lưu ý: tên project `DataAcessLayer` đang giữ nguyên theo solution hiện tại.

## Role và phân quyền

DocXM có hai role chính:

- `Student`: người học, chỉ xem và chat với Subject được cấp quyền.
- `Instructor`: giảng viên, tạo Subject, upload tài liệu, cấp/thu hồi quyền cho Student.

Quy tắc hiện tại:

- User đăng ký mới mặc định là `Student`.
- Người dùng không tự chọn được `Instructor` khi đăng ký.
- Tài khoản `Instructor` cần được cấp riêng trong môi trường dev bằng seed hoặc update database.
- Student chỉ thấy Subject nếu có bản ghi tương ứng trong `SubjectPermissions`.
- Student chỉ thấy Document, preview/download và chat trong phạm vi Subject đã được cấp quyền.
- Instructor chỉ thao tác với Subject/Document do mình sở hữu/upload.

## Luồng chính

1. Instructor đăng nhập.
2. Instructor tạo Subject.
3. Instructor upload Document PDF/DOCX/PPTX vào Subject.
4. Hệ thống validate file, lưu file gốc, extract text, chunking, embedding và lưu vector.
5. Instructor cấp quyền xem Subject cho Student bằng email.
6. Student đăng nhập và thấy Subject/Document được cấp quyền.
7. Student preview PDF hoặc download file gốc.
8. Student hỏi chatbot trong phạm vi Subject/Chapter/Document được phép.
9. Instructor thu hồi quyền.
10. Student mất quyền xem Subject/Document/preview/download/chat trong scope đó.

## Tạo tài khoản Instructor trong môi trường dev

Trong môi trường phát triển:

1. Đăng ký tài khoản bình thường trên giao diện Register.
2. Tài khoản mới sẽ là `Student`.
3. Update role của tài khoản đó trong database thành `Instructor`.

Ví dụ thao tác SQL cần dùng giá trị phù hợp với database local của bạn:

```sql
UPDATE AppUsers
SET Role = 'Instructor'
WHERE Email = '<email-dev>';
```

Không ghi password thật, API key hoặc thông tin đăng nhập vào README, source code hoặc commit.

## Database

DocXM dùng SQL Server. Các script database nằm trong thư mục `database`.

Các file thường dùng:

- `database/DocXM.sql`: script schema chính.
- `database/RecreateDocXM.sql`: tạo lại database/schema.
- `database/ResetDocXM.sql`: reset môi trường dev.
- Các file `Hotfix_*.sql`: cập nhật nhỏ theo từng giai đoạn.

Các bảng/cụm bảng quan trọng:

- `AppUsers`
- `Subjects`
- `SubjectPermissions`
- `Chapters`
- `Documents`
- `DocumentChunks`
- `EmbeddingModels`
- `DocumentEmbeddings`
- `ChatConversations`
- `ChatMessages`
- `ChatCitations`

Lưu ý: `SubjectPermissions` phải tồn tại để Student có thể thấy Subject, Document và chat trong phạm vi được cấp quyền.

## AI/RAG

Luồng xử lý tài liệu:

```text
Upload file
  -> Validate extension/content type/signature
  -> Lưu file gốc
  -> Extract text
  -> Chunking
  -> Embedding
  -> Lưu metadata vào SQL Server
  -> Lưu vector vào vector store
```

Luồng chatbot:

```text
Câu hỏi
  -> Kiểm tra quyền Subject/Chapter/Document
  -> Tạo embedding câu hỏi
  -> Search vector store + keyword search
  -> Rerank chunk
  -> Build context nội bộ
  -> Generate answer
  -> Lưu conversation/message/citation
```

Hiện tại:

- PDF được trích xuất bằng PdfPig.
- DOCX/PPTX được trích xuất bằng OpenXML.
- Text được chia thành chunk để phục vụ retrieval.
- Vector store hiện tại là local JSON tại `Presentation/App_Data/vector-store/vectors.json`.
- Nếu Gemini được cấu hình hợp lệ, hệ thống dùng Gemini cho embedding và answer generation.
- Nếu Gemini chưa cấu hình, hệ thống dùng fallback local để demo offline.
- RAG citation lưu `DocumentId`, `DocumentChunkId`, tên tài liệu, snippet ngắn và score.

## Bảo mật

- Register không cho tự chọn `Instructor`.
- Quyền được kiểm tra ở controller/service/repository.
- Student chỉ truy cập dữ liệu qua `SubjectPermissions`.
- Instructor chỉ truy cập Subject/Document do mình sở hữu/upload.
- Preview/download file phải đi qua `DocumentController`.
- Không expose đường dẫn thật của file trong `wwwroot/uploads`.
- Direct request tới `/uploads/*` bị chặn trước static file middleware.
- Preview hiện chỉ hỗ trợ PDF.
- DOCX/PPTX không preview trực tiếp; người dùng được nhắc tải file gốc.
- RAG debug logging mặc định tắt.
- Khi bật RAG debug, hệ thống chỉ log metadata an toàn, không log full question, prompt, context, answer hoặc nội dung tài liệu.
- Không commit API key, password, token hoặc connection string thật.

## Cấu hình

### Connection string

Cấu hình SQL Server trong `Presentation/appsettings.json`, user secrets hoặc biến môi trường.

Khuyến nghị dùng user secrets/biến môi trường cho môi trường dev cá nhân:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-local-connection-string>" --project Presentation\Presentation.csproj
```

### Gemini

Không commit API key. Có thể bật Gemini bằng biến môi trường hoặc user secrets:

```powershell
dotnet user-secrets set "Gemini:Enabled" "true" --project Presentation\Presentation.csproj
dotnet user-secrets set "Gemini:ApiKey" "<your-api-key>" --project Presentation\Presentation.csproj
```

Nếu không cấu hình Gemini, fallback local vẫn chạy.

### RAG debug

Mặc định:

```json
"RagDebug": {
  "Enabled": false
}
```

Chỉ bật khi cần chẩn đoán. Kể cả khi bật, log chỉ chứa metadata an toàn.

## Cách chạy

Restore package:

```powershell
dotnet restore .\DocxM.sln
```

Build solution:

```powershell
dotnet build .\DocxM.sln
```

Cấu hình connection string cho SQL Server bằng user secrets, biến môi trường hoặc appsettings local không commit.

Chạy script database phù hợp trong thư mục `database` bằng SQL Server Management Studio, Azure Data Studio hoặc công cụ SQL Server CLI trong môi trường của bạn.

Chạy project MVC:

```powershell
dotnet run --project .\Presentation\Presentation.csproj
```

Route mặc định:

```text
/Subject/Index
```

## Kịch bản demo ngắn

1. Tạo hoặc chuẩn bị tài khoản Instructor trong database dev.
2. Đăng nhập bằng Instructor.
3. Tạo một Subject.
4. Upload một file PDF.
5. Đăng nhập bằng Student chưa được cấp quyền: Student chưa thấy Subject/Document đó.
6. Instructor cấp quyền Subject cho Student bằng email.
7. Student đăng nhập lại hoặc refresh: thấy Subject/Document.
8. Student mở preview PDF, download file gốc và hỏi chatbot về nội dung tài liệu.
9. Instructor thu hồi quyền.
10. Student refresh: mất Subject/Document và không còn preview/download/chat được trong scope đó.

## Lưu ý phát triển

- Không đổi database schema nếu task chỉ yêu cầu UI/logic.
- Không đổi RAG/vector store nếu task không yêu cầu.
- Không đưa file upload thật hoặc vector store chứa dữ liệu nhạy cảm vào commit.
- Khi thay đổi schema, cập nhật script trong `database` và kiểm tra lại entity/repository liên quan.
- Sau mỗi thay đổi logic quan trọng, chạy:

```powershell
dotnet build .\DocxM.sln
```
