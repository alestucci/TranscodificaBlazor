using Microsoft.AspNetCore.Components.Forms;

namespace TranscodificaBlazor.Components.Service
{
    public interface IService
    {
        bool IsValidExcel(IBrowserFile file);
        Task<Dictionary<string, string>?> ProcessaFileCsv(IBrowserFile file);
        Task<(string json, string csv)> TranscodificaAsync(Dictionary<string, string> countries, IBrowserFile excelfile);
    }
}