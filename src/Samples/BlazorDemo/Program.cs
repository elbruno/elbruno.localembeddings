using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register the shared embedding state service
builder.Services.AddLocalEmbeddingsBlazorComponents();

// Register a local embedding generator (downloads all-MiniLM-L6-v2 on first run)
builder.Services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.EnsureModelDownloaded = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
