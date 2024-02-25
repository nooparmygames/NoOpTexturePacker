using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

//We use this object for the console logging when parallel processors want to write to console
object ConsoleLock = new();

string? SearchExtension = "";
string? path = null;

if (args.Length > 1)
{
    path = args[1];
}
if (args.Length > 2)
{
    SearchExtension = args[2];
}

if (string.IsNullOrEmpty(path))
{
    Console.WriteLine("Enter a path to search");
    path = Console.ReadLine();
}
if (string.IsNullOrEmpty(path))
{
    Console.WriteLine("You need to enter a valid path");
    return;
}
if (string.IsNullOrEmpty(SearchExtension))
{
    Console.WriteLine("Enter the extension to search without the initial . so png and not .png");
    SearchExtension = Console.ReadLine();
}
if (string.IsNullOrEmpty(SearchExtension) || (!SearchExtension.Equals("png", StringComparison.CurrentCultureIgnoreCase) && !SearchExtension.Equals("jpg", StringComparison.CurrentCultureIgnoreCase) && !SearchExtension.Equals("exr", StringComparison.CurrentCultureIgnoreCase)))
{
    SearchExtension = "png";
    Console.WriteLine("Moving forward with png");
}

bool ShouldSaveUnityORM = true;
bool ShouldSaveUnrealORM = true;
bool ShouldSaveUnitySmoothnessInMetallic = true;
bool ShouldDeleteNonORMFiles = false;
if (!(args.Length > 3 && bool.TryParse(args[3], out ShouldSaveUnityORM)))
{
    ShouldSaveUnityORM = GetYesOrNoAnswerFromConsole("Should save Unity ORM? Y/N");
}
if (!(args.Length > 4 && bool.TryParse(args[4], out ShouldSaveUnrealORM))) if (!(args.Length > 4 && bool.TryParse(args[4], out ShouldSaveUnrealORM)))
    {
        ShouldSaveUnrealORM = GetYesOrNoAnswerFromConsole("Should save Unreal ORM? Y/N");
    }
if (!(args.Length > 4 && bool.TryParse(args[4], out ShouldSaveUnrealORM))) if (!(args.Length > 5 && bool.TryParse(args[5], out ShouldSaveUnitySmoothnessInMetallic)))
    {
        ShouldSaveUnitySmoothnessInMetallic = GetYesOrNoAnswerFromConsole("Should save Unity Smoothness from inverse of roughness to alpha of metallic texture? Y/N");
    }
if (!(args.Length > 4 && bool.TryParse(args[4], out ShouldSaveUnrealORM))) if (!(args.Length > 6 && bool.TryParse(args[6], out ShouldDeleteNonORMFiles)))
    {
        ShouldDeleteNonORMFiles = GetYesOrNoAnswerFromConsole("Should DELETE roughness, metallic and AO textures after the operations are done? Y/N");
    }

//Search the directory specifiedfor files
string[] paths = Directory.GetFiles(path, $"*.{SearchExtension}", SearchOption.AllDirectories);
Console.WriteLine($"Files found: {paths.Length}");
Dictionary<string, List<string>> FilesGroupedByDirectory = [];

//put files of a single directory in a single dictionary key for easy processing
foreach (string p in paths)
{
    string? dir = Path.GetDirectoryName(p);
    if (string.IsNullOrEmpty(dir))
        throw new Exception("The file directory should not be null");

    string Name = Path.GetFileName(p);
    if (!FilesGroupedByDirectory.TryGetValue(dir, out List<string>? InDirectoryFileList))
    {
        InDirectoryFileList = ([]);
        FilesGroupedByDirectory.Add(dir, InDirectoryFileList);
    }

    InDirectoryFileList.Add(p);
}

Stopwatch sw = new Stopwatch();
sw.Start();
//Process all images
Parallel.ForEach(FilesGroupedByDirectory.Keys, Dir =>
{
    var PBRFilesList = FilesGroupedByDirectory[Dir];
    string? RoughnessFile = PBRFilesList.FirstOrDefault(x => x.Contains("roughness", StringComparison.CurrentCultureIgnoreCase));
    string? MetallicFile = PBRFilesList.FirstOrDefault(x => x.Contains("metallic", StringComparison.CurrentCultureIgnoreCase));
    string? AOFile = PBRFilesList.FirstOrDefault(x => x.Contains("ao", StringComparison.CurrentCultureIgnoreCase));
    if (string.IsNullOrEmpty(MetallicFile) || string.IsNullOrEmpty(RoughnessFile) || string.IsNullOrEmpty(AOFile))
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"the folder {Dir} does not contain all required maps. The files are as follows");
            foreach (var file in PBRFilesList)
            {
                Console.WriteLine($"\t{file}");
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
        return;
    }
    else
    {
        Image<Argb32>? AO = null;
        Image<Argb32>? Roughness = null;
        Image<Argb32>? Metallic = null;
        Image<Rgb24>? ORMUE = null;
        Image<Rgb24>? ORMUnity = null;
        Image<Argb32>? NewMetallic = null;
        try
        {
            AO = Image.Load<Argb32>(AOFile);
            Roughness = Image.Load<Argb32>(RoughnessFile);
            Metallic = Image.Load<Argb32>(MetallicFile);
            ORMUE = new Image<Rgb24>(AO.Width, AO.Height);
            ORMUnity = new Image<Rgb24>(AO.Width, AO.Height);
            NewMetallic = new Image<Argb32>(AO.Width, AO.Height);


            for (int j = 0; j < Roughness.Height; j++)
            {
                for (int i = 0; i < Roughness.Width; i++)
                {

                    if (ShouldSaveUnityORM || ShouldSaveUnitySmoothnessInMetallic)
                    {
                        Argb32 CurrentMetallicPixel = Metallic[i, j];
                        byte InverseRoughness = (byte)(255 - Roughness[i, j].R);
                        CurrentMetallicPixel.A = InverseRoughness;
                        NewMetallic[i, j] = CurrentMetallicPixel;
                        if (ShouldSaveUnityORM)
                        {
                            ORMUnity[i, j] = new Rgb24(AO[i, j].R, InverseRoughness, Metallic[i, j].R);
                        }
                    }
                    if (ShouldSaveUnrealORM)
                    {
                        ORMUE[i, j] = new Rgb24(AO[i, j].R, Roughness[i, j].R, Metallic[i, j].R);
                    }
                }
            }
            if (ShouldSaveUnitySmoothnessInMetallic)
            {
                NewMetallic.Save(MetallicFile);
            }
            if (ShouldSaveUnrealORM)
            {
                ORMUE.Save(Path.Combine(Dir, $"{Path.GetFileName(MetallicFile.Replace("Metallic", "ormue"))}"));
            }
            if (ShouldSaveUnityORM)
            {
                ORMUnity.Save(Path.Combine(Dir, $"{Path.GetFileName(MetallicFile.Replace("Metallic", "ormunity"))}"));
            }
            if (ShouldDeleteNonORMFiles)
            {
                if (File.Exists(RoughnessFile))
                {
                    File.Delete(RoughnessFile);
                }
                if (File.Exists(AOFile))
                {
                    File.Delete(AOFile);
                }
                if (File.Exists(MetallicFile))
                {
                    File.Delete(MetallicFile);
                }
            }
        }
        finally
        {
            AO?.Dispose();
            Roughness?.Dispose();
            Metallic?.Dispose();
        }
    }
    lock (ConsoleLock)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Porcessed {Dir} successfully!");
        Console.ForegroundColor = ConsoleColor.White;
    }
});
Console.Write($"Took {sw.ElapsedMilliseconds} ms");
//Finished proccessing

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Done!");
Console.ForegroundColor = ConsoleColor.White;

//Gets a yes or no answer from the console
static bool GetYesOrNoAnswerFromConsole(string message)
{
    string? YesNoAnswer;
    Console.WriteLine(message);
    YesNoAnswer = Console.ReadLine();
    YesNoAnswer ??= "";

    {

    }
    if (YesNoAnswer.Equals("y", StringComparison.CurrentCultureIgnoreCase) || YesNoAnswer.Equals("yes", StringComparison.CurrentCultureIgnoreCase))
    {
        return true;
    }
    else if (YesNoAnswer.Equals("n", StringComparison.CurrentCultureIgnoreCase) || YesNoAnswer.Equals("no", StringComparison.CurrentCultureIgnoreCase))
    {
        return false;
    }
    return false;
}