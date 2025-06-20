using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Random = System.Random;

namespace Conay.ViewModels;

public class SplashScreenViewModel : ViewModelBase
{
    private readonly string[] _images =
    [
        "amelot", "bard", "boar", "bossfight", "campfire",
        "circus", "ciri", "jousting", "kyorlin3", "kyorlin4",
        "laeticia", "night", "ship", "tasha", "thorne",
        "cat", "bae", "cave", "drow", "shivix1", "shivix2",
        "bear"
    ];

    public Bitmap GetRandomImage =>
        new(AssetLoader.Open(
            new Uri($"avares://Conay/Assets/Images/Splash/{_images[new Random().Next(_images.Length)]}.jpg")));
}