# Giải thích source code DocXM

Tài liệu này giải thích tổng quan source code DocXM để người mới có thể đọc dự án theo đúng luồng nghiệp vụ. Tài liệu không ghi lại password, API key, token hoặc connection string thật; các cấu hình nhạy cảm nếu có trong môi trường local cần được mask và không commit.

## 1. Giới thiệu hệ thống

DocXM là hệ thống ASP.NET Core MVC .NET 8 dùng để quản lý tài liệu học tập theo môn học. Người dùng chính gồm:

- `Instructor`: tạo môn học, upload tài liệu, quản lý quyền truy cập của sinh viên.
- `Student`: xem tài liệu và hỏi chatbot trong các môn học được cấp quyền.

Các chức năng chính:

- Đăng ký, đăng nhập, đăng xuất bằng cookie authentication.
- Quản lý Subject/Chapter/Document.
- Upload tài liệu PDF/DOCX/PPTX.
- Preview PDF trực tiếp trên web.
- Download file gốc qua controller có kiểm tra quyền.
- Cấp/thu hồi quyền Student xem Subject.
- Chatbot RAG hỏi đáp theo phạm vi Subject/Chapter/Document được phép.

## 2. Công nghệ sử dụng

- ASP.NET Core MVC .NET 8.
- Razor Views cho giao diện server-rendered.
- Entity Framework Core với SQL Server.
- Cookie Authentication để đăng nhập và lưu role/user claims.
- Session để ghi nhớ conversation hiện tại trong trang Chat.
- PdfPig để đọc PDF.
- OpenXML SDK để đọc DOCX/PPTX.
- AI/RAG module gồm extract text, chunking, embedding, vector search, keyword search, rerank và answer generation.
- Gemini nếu được cấu hình hợp lệ; fallback local nếu chưa cấu hình Gemini.

## 3. Cấu trúc solution/project

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

- `Presentation`: project ASP.NET Core MVC, chứa `Program.cs`, controllers, view models, Razor views, static files trong `wwwroot`, current-user helper và cấu hình app.
- `BusinessLogic`: chứa service nghiệp vụ, DTO, validation, upload rules, file storage và dependency injection cho business layer.
- `DataAcessLayer`: chứa `AppDbContext` và repository truy cập database.
- `BusinessObjects`: chứa entity, enum và hằng số role dùng chung.
- `AIService`: chứa service validate chữ ký file, extract text, chunking, embedding, vector store và answer generation.
- `database`: chứa script SQL Server để tạo/cập nhật schema.
- `wwwroot`: chứa static assets và thư mục upload vật lý, nhưng request trực tiếp `/uploads/*` bị chặn.

## 4. Kiến trúc tổng thể

Luồng phụ thuộc chính:

```text
Razor View
  -> Controller (Presentation)
      -> Service (BusinessLogic)
          -> Repository (DataAcessLayer)
              -> Entity (BusinessObjects)
          -> AIService
```

Controller không gọi trực tiếp `DbContext`. Controller nhận request, lấy thông tin user hiện tại, build DTO/filter và gọi service. Service xử lý nghiệp vụ, validate, kiểm tra quyền và điều phối repository/AI service. Repository chứa truy vấn EF Core và lọc dữ liệu theo Instructor/Student. Entity nằm trong `BusinessObjects`. `AIService` phụ trách các bước liên quan đến file và RAG: extract, chunk, embedding, vector store và tạo câu trả lời.

## 5. Role và phân quyền

DocXM có hai role:

- `Student`
- `Instructor`

Quy tắc chính:

- User đăng ký mới mặc định là `Student`.
- Form đăng ký không cho tự chọn `Instructor`.
- `Instructor` cần được cấp riêng bằng seed hoặc update database trong môi trường dev.
- `Instructor` tạo Subject và upload Document.
- `Student` chỉ thấy Subject nếu có bản ghi trong `SubjectPermissions`.
- Preview, download và chat đều phải đi qua kiểm tra quyền.
- Repository thường lọc theo `CreatedByUserId`/`UploadedByUserId` cho Instructor và `SubjectPermissions` cho Student.

## 6. Luồng đăng ký/đăng nhập

File/class chính:

- `Presentation/Controllers/AccountController.cs`
- `BusinessLogic/Services/AuthService.cs`
- `BusinessObjects/Entities/AppUser.cs`
- `BusinessObjects/UserRoles.cs`

Luồng đăng ký:

1. View Register gửi `RegisterViewModel` về `AccountController.Register`.
2. Controller gọi `AuthService.RegisterAsync`.
3. `AuthService` validate username/email/password.
4. User mới được tạo với role `Student`.
5. Password được hash bằng `IPasswordHasher`.
6. User được lưu qua `UserRepository`.
7. Controller sign-in bằng cookie claims.

Luồng đăng nhập:

1. `AccountController.Login` gọi `AuthService.LoginAsync`.
2. `AuthService` tìm user theo username/email normalized.
3. Kiểm tra user active và verify password hash.
4. Controller ghi cookie claims gồm user id, username, email, role và full name.

## 7. Luồng quản lý Subject

File/class chính:

- `Presentation/Controllers/SubjectController.cs`
- `BusinessLogic/Services/SubjectService.cs`
- `DataAcessLayer/Repositories/SubjectRepository.cs`
- `BusinessObjects/Entities/Subject.cs`
- `BusinessObjects/Entities/SubjectPermission.cs`

Luồng chính:

- Instructor tạo Subject bằng `CreateSubjectAsync`.
- Instructor xem/sửa/xóa Subject do mình tạo.
- Subject đang có chapter/document thì không được xóa để tránh mất dữ liệu liên quan.
- Instructor mở trang permission để cấp quyền Student theo email.
- `SubjectService.GrantSubjectPermissionAsync` chỉ cấp cho user có role Student, không cấp trùng và không cấp nếu email không tồn tại.
- `RevokeSubjectPermissionAsync` xóa quyền khỏi `SubjectPermissions`.
- Student chỉ thấy Subject còn permission hợp lệ.

## 8. Luồng quản lý Document

File/class chính:

- `Presentation/Controllers/DocumentController.cs`
- `BusinessLogic/Services/DocumentService.cs`
- `BusinessLogic/Services/FileStorageService.cs`
- `BusinessLogic/Services/DocumentProcessingService.cs`
- `AIService/Services/FileSignatureValidator.cs`
- `AIService/Services/DocumentTextExtractor.cs`
- `AIService/Services/TextChunkingService.cs`
- `DataAcessLayer/Repositories/DocumentRepository.cs`

Luồng upload:

1. Instructor mở form upload.
2. Controller nhận file và gọi `DocumentService.UploadDocumentAsync`.
3. Service validate Subject thuộc Instructor hiện tại.
4. Validate metadata, extension, content type và file size.
5. File được lưu tạm bằng `FileStorageService`.
6. `FileSignatureValidator` kiểm tra chữ ký file thật và khả năng mở file.
7. Repository lấy hoặc tạo Chapter theo tên chương.
8. File được move vào thư mục document.
9. `DocumentProcessingService` extract text, chunk nội dung và set metadata xử lý.
10. Metadata/chunk lưu vào SQL Server.
11. Vector được lưu vào vector store nếu tài liệu sẵn sàng.

Preview/download:

- `Download` gọi `GetDocumentFileAsync`, kiểm tra quyền rồi trả `PhysicalFile`.
- `Preview` dùng cùng đường kiểm tra quyền với download.
- Preview hiện chỉ hỗ trợ PDF và trả inline với range processing.
- DOCX/PPTX chưa preview trực tiếp; người dùng được nhắc tải file gốc.
- Physical path được resolve bằng `GetSafePhysicalPath` để chống path traversal.

Reindex/delete:

- Reindex chỉ cho Instructor upload tài liệu đó.
- Reindex xóa vector cũ, clear chunk cũ, extract/chunk/store vector lại.
- Delete xóa database record, vector liên quan và file vật lý.

## 9. Luồng chatbot RAG

File/class chính:

- `Presentation/Controllers/ChatController.cs`
- `BusinessLogic/Services/ChatService.cs`
- `DataAcessLayer/Repositories/ChatRepository.cs`
- `AIService/Services/LocalJsonVectorStore.cs`
- `AIService/Services/GeminiAnswerGenerationService.cs`
- `AIService/Services/PromptAnswerGenerationService.cs`

Luồng chat:

1. User chọn scope Subject/Chapter/Document trên trang Chat.
2. Controller truyền `OwnerUserId` nếu Instructor hoặc `ViewerUserId` nếu Student.
3. `ChatService` validate Subject/Chapter/Document theo quyền hiện tại.
4. Nếu chọn document cụ thể, document phải thuộc scope và đã indexed.
5. Nếu không chọn document, service lấy các document indexed trong scope.
6. Service tạo embedding cho câu hỏi.
7. Vector store search theo document ids được phép.
8. Keyword search lấy thêm ứng viên theo từ khóa.
9. Service rerank chunk theo điểm lexical/vector.
10. Chọn context chunk, build prompt nội bộ.
11. Gọi Gemini nếu cấu hình; nếu lỗi hoặc chưa cấu hình thì dùng fallback local.
12. Lưu conversation, message và citation.
13. UI hiển thị câu trả lời và nguồn trích dẫn.

Conversation cũ:

- Conversation chỉ load messages nếu scope còn hợp lệ với quyền hiện tại.
- Nếu Student bị revoke quyền, subject/document không còn lọt qua query permission, nên conversation cũ không dùng lại được trong scope đó.

RAG debug:

- Mặc định tắt bằng `RagDebug.Enabled = false`.
- Khi bật, logging chỉ dùng metadata an toàn như ids, số chunk, chunk ids, score, intent và độ dài question/prompt/answer.
- Không log full question, prompt, context, answer hoặc nội dung tài liệu.

## 10. Database/entity chính

- `AppUser`: tài khoản, role, password hash, trạng thái active và navigation đến subject/document/chat/permission.
- `Subject`: môn học, thuộc Instructor qua partial ownership, có chapters/documents/permissions.
- `SubjectPermission`: quyền Student được xem một Subject, do Instructor cấp.
- `Chapter`: chương thuộc Subject, thường được tạo khi upload tài liệu theo tên chương.
- `Document`: metadata file upload, đường dẫn lưu trữ, trạng thái xử lý, số chunk.
- `DocumentChunk`: đoạn nội dung đã chia, section title, token count, vector id.
- `EmbeddingModel`: metadata model embedding.
- `DocumentEmbedding`: mapping chunk với embedding model.
- `ChatConversation`: phiên hội thoại theo user và scope Subject/Chapter/Document.
- `ChatMessage`: câu hỏi người dùng và câu trả lời AI.
- `ChatCitation`: nguồn trích dẫn cho câu trả lời, trỏ về document/chunk.

## 11. Các service/repository quan trọng

- `AuthService`: đăng ký, đăng nhập, validate user và hash/verify password.
- `SubjectService`: quản lý Subject, cấp/thu hồi quyền Student.
- `DocumentService`: upload, validate, preview/download metadata, reindex, delete.
- `FileStorageService`: lưu file tạm, move file, resolve safe physical path, delete file.
- `DocumentProcessingService`: extract text, chunk tài liệu, lưu/xóa vector.
- `ChatService`: validate scope chat, retrieve/rerank chunk, build prompt, tạo answer, lưu citation.
- `CurrentUserService`: đọc user id/email/role từ cookie claims.
- `UserRepository`: truy vấn user theo id/username/email normalized.
- `SubjectRepository`: truy vấn Subject/Permission theo owner hoặc permission.
- `DocumentRepository`: truy vấn Document/Chapter/Subject cho document workflow.
- `ChatRepository`: truy vấn Subject/Document/Chunk/Conversation phục vụ chatbot, luôn lọc theo role/scope.

## 12. Bảo mật hiện tại

- User mới luôn là Student, không tự nâng quyền Instructor từ form đăng ký.
- Các action Instructor-only dùng role-based authorization.
- Query dữ liệu lọc theo `CreatedByUserId`, `UploadedByUserId` hoặc `SubjectPermissions`.
- Preview/download đi qua `DocumentController` và `DocumentService.GetDocumentFileAsync`.
- Direct `/uploads/*` bị chặn trước static file middleware.
- `FileStorageService.GetSafePhysicalPath` đảm bảo path cuối cùng vẫn nằm dưới web root.
- RAG debug mặc định tắt và không log nội dung nhạy cảm khi bật.
- Gemini/API key/connection string nên cấu hình bằng user secrets hoặc biến môi trường, không commit.

## 13. Hạn chế hiện tại

- DOCX/PPTX chưa preview trực tiếp trên web, hiện chỉ tải file gốc.
- Vector store local JSON phù hợp demo/dev hơn production.
- Database đang dùng SQL script thủ công; nếu sản xuất nên cân nhắc quy trình migration/upgrade rõ ràng.
- `ChatService` đang tập trung nhiều bước RAG trong một class lớn; có thể tách nhỏ dần nếu nhu cầu mở rộng tăng.
- Cần thêm automated tests cho auth, permission, upload, preview/download và RAG để production-ready hơn.

## 14. Kịch bản demo đề xuất

1. Đăng ký một tài khoản Student.
2. Tạo/cấp một tài khoản Instructor trong database dev.
3. Instructor đăng nhập và tạo Subject.
4. Instructor upload một file PDF.
5. Student chưa được cấp quyền đăng nhập và chưa thấy Subject đó.
6. Instructor cấp quyền Subject cho Student theo email.
7. Student refresh, thấy Subject/Document.
8. Student preview PDF, download file gốc và hỏi chatbot.
9. Instructor thu hồi quyền.
10. Student refresh và mất quyền xem/preview/download/chat trong Subject đó.
