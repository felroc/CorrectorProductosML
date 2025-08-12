using CorrectorProductos;

var builder = WebApplication.CreateBuilder(args);

// Ruta del modelo
var modelPath = Path.Combine(AppContext.BaseDirectory, "wordCorrectionModel.zip");
var excelPath = Path.Combine(AppContext.BaseDirectory, "productos.csv");


var stringConnection1 = @"Server=192.200.9.131; user=sa; password=bofasa1$; Min Pool Size=10;Max Pool Size=30; Integrated Security=False; Trust Server Certificate=true;";
var stringConnection2 = @"Server=.\sqlexpress; user=sa; password=Pruebas123; Min Pool Size=10;Max Pool Size=30; Integrated Security=False; Trust Server Certificate=true;";


// Agregar el servicio 
builder.Services.AddSingleton(new WordCorrectionService(modelPath, excelPath, stringConnection1, stringConnection2));

// en launchSetting.json
// "launchUrl": "api/correct/pedialit",

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
