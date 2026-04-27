# Letter Template Generator

A full-stack ASP.NET Core MVC web application for generating dynamic letters using custom templates with placeholder replacement functionality.

## Features

- **Template Management**: Create, edit, and delete letter templates with rich text editing using TinyMCE
- **Custom Interpreter Engine**: Mustache-like syntax for placeholder replacement with repeat block support
- **Dynamic Letter Generation**: Generate personalized letters for customers with multiple loans
- **Database Integration**: PostgreSQL with Entity Framework Core migrations
- **Sample Data**: Pre-seeded with customers, loans, guarantors, and sample templates
- **Modern UI**: Bootstrap 5 with Font Awesome icons and responsive design
- **API Endpoints**: RESTful API for template processing and letter generation
- **Unit Tests**: Comprehensive tests for the interpreter engine

## Tech Stack

- **Backend**: ASP.NET Core MVC + Web API
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core + Npgsql
- **Frontend**: Razor views + Bootstrap 5 + jQuery
- **Rich Text Editor**: TinyMCE
- **Template Engine**: Custom mustache-like interpreter
- **Testing**: xUnit

## Quick Start

### Prerequisites

- .NET 8.0 SDK or later
- PostgreSQL server running on localhost
- Visual Studio 2022 or VS Code

### Setup Instructions

1. **Clone and Build**
   ```bash
   cd D:\repos\LetterTemplatePractice
   dotnet restore
   dotnet build
   ```

2. **Configure Database**
   - Ensure PostgreSQL is running on localhost:5432
   - Update connection string in `appsettings.json` if needed:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=LetterTemplateDb;Username=postgres;Password=postgres"
   }
   ```

3. **Apply Migrations**
   ```bash
   dotnet ef database update
   ```
   *Note: The application will automatically seed sample data on first run*

4. **Run the Application**
   ```bash
   dotnet run
   ```

5. **Access the Application**
   - Navigate to `https://localhost:7xxx` (check console for actual port)
   - Swagger UI available at `/swagger` in development mode

## Usage Guide

### Template Syntax

The custom interpreter supports the following syntax:

#### Simple Placeholders
```
{{personal:name-data}}
{{facility:loanAccountNumber-data}}
{{guarantor:name-data}}
```

#### Indexed Access (for arrays)
```
{{facility:0-loanAccountNumber-data}}  // First facility
{{facility:1-loanAccountNumber-data}}  // Second facility
```

#### Repeat Blocks
```
{{RPT:R-facility}}
Account: {{facility:loanAccountNumber-data}}
Principal: {{facility:principal-data}}
{{RPT:END-facility}}

{{RPT:R-guarantor}}
Guarantor: {{guarantor:name-data}}
{{RPT:END-guarantor}}
```

### Available Data Groups

- **personal**: Customer information (name, address, phone, etc.)
- **facility**: Array of loan information (account numbers, amounts, etc.)
- **guarantor**: Array of guarantor information
- **property**: Array of property information (empty by default)

### Sample Placeholders

```
{{personal:name-data}}          // Customer name (English)
{{personal:name-nepali}}        // Customer name (Nepali)
{{personal:phone-data}}         // Phone number
{{personal:address-data}}       // Address (English)
{{personal:address-nepali}}     // Address (Nepali)

{{facility:loanAccountNumber-data}}  // Loan account number
{{facility:mainCode-data}}            // Main code
{{facility:principal-data}}           // Principal amount
{{facility:interest-data}}            // Interest rate
{{facility:penal-data}}               // Penal interest
{{facility:overdue-data}}             // Overdue amount

{{guarantor:name-data}}         // Guarantor name
{{guarantor:phone-data}}       // Guarantor phone
{{guarantor:relationship-data}} // Relationship
```

## API Endpoints

### Template Management
- `GET /templates` - List all templates
- `POST /templates/create` - Create new template
- `GET /templates/edit/{id}` - Get template for editing
- `POST /templates/edit/{id}` - Update template
- `POST /templates/delete/{id}` - Delete template

### Letter Generation
- `GET /letters/generate` - Letter generation page
- `GET /letters/getcustomerloans/{customerId}` - Get customer's loans
- `POST /api/letters/generate` - Generate letter (returns HTML)
- `GET /letters/history/{customerId}` - View generated letters history
- `GET /letters/preview/{id}` - Preview specific letter

### Interpreter API
- `GET /api/interpreter/keys` - Get all available placeholder keys
- `POST /api/interpreter/test` - Test template processing

## Sample Templates

The application includes three pre-configured templates:

1. **First Notice Letter** - Initial overdue notice
2. **Final Warning Letter** - Urgent final warning with legal consequences
3. **Payment Reminder** - Gentle monthly reminder

## Testing

Run the unit tests:
```bash
dotnet test LetterTemplatePractice.Tests
```

The test suite covers:
- Simple placeholder replacement
- Missing placeholder handling
- Repeat block processing
- Indexed array access
- Complex template scenarios
- Key extraction functionality

## Project Structure

```
LetterTemplatePractice/
├── Controllers/
│   ├── Api/
│   │   └── InterpreterController.cs
│   ├── TemplateController.cs
│   └── LetterController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── DataSeeder.cs
├── Models/
│   ├── Customer.cs
│   ├── Loan.cs
│   ├── Guarantor.cs
│   ├── FollowUp.cs
│   ├── Template.cs
│   └── GeneratedLetter.cs
├── Services/
│   ├── TemplateService.cs
│   ├── PayloadBuilderService.cs
│   └── Interpreter/
│       ├── Token.cs
│       ├── Tokenizer.cs
│       ├── Parser.cs
│       ├── Executor.cs
│       └── InterpreterService.cs
├── Views/
│   ├── Template/
│   │   ├── Index.cshtml
│   │   ├── Create.cshtml
│   │   └── Edit.cshtml
│   └── Letter/
│       ├── Generate.cshtml
│       ├── History.cshtml
│       └── Preview.cshtml
└── Program.cs
```

## Database Schema

### Tables
- **Customers** - Customer personal information
- **Loans** - Loan details and financial information
- **Guarantors** - Loan guarantor information
- **FollowUps** - Follow-up tracking
- **Templates** - Letter templates with HTML content
- **GeneratedLetters** - Generated letter history

### Relationships
- Customer → Loans (1:N)
- Loan → Guarantors (1:N)
- Loan → FollowUps (1:N)
- Customer → GeneratedLetters (1:N)
- Template → GeneratedLetters (1:N)

## Development Notes

### Adding New Templates
1. Navigate to `/templates/create`
2. Fill in template details
3. Use TinyMCE editor for HTML content
4. Use available keys sidebar for placeholder insertion
5. Save template

### Generating Letters
1. Navigate to `/letters/generate`
2. Select customer from dropdown
3. Select one or more loans
4. Choose template
5. Click "Generate Letter"
6. Preview, save, print, or download

### Customizing Interpreter
The interpreter engine is modular and can be extended:
- **Tokenizer**: Parses template string into tokens
- **Parser**: Converts tokens into structured representation
- **Executor**: Processes tokens with payload data

## Troubleshooting

### Database Connection Issues
- Ensure PostgreSQL is running
- Check connection string in appsettings.json
- Verify database exists and credentials are correct

### Template Processing Errors
- Check placeholder syntax matches expected format
- Ensure payload contains required data groups
- Verify repeat blocks are properly closed

### Missing Dependencies
- Run `dotnet restore` to restore NuGet packages
- Ensure all required packages are installed

## License

This project is for educational and practice purposes.

## Contributing

Feel free to extend the functionality:
- Add new template features
- Improve the interpreter engine
- Enhance the UI/UX
- Add more sample data
- Implement additional API endpoints
