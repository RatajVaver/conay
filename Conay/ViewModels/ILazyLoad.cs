using System.Threading.Tasks;

namespace Conay.ViewModels;

public interface ILazyLoad
{
    public bool IsLoaded { get; set; }
    public bool IsVisible { get; set; }
    public Task LoadDataAsync();
}