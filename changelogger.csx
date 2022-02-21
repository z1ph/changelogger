#!/usr/bin/env dotnet-script

#r "nuget: Serilog, 2.10.0"
#r "nuget: Serilog.Sinks.Console, 4.0.1"
#r "nuget: Serilog.Sinks.File, 5.0.0"
#r "nuget: YamlDotNet, 11.2.1"

#nullable enable

using System.Reflection;
using System.Runtime.Serialization;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

const string VersionHeader = "## ";
const string GeneralHeader = "### 📣 General";
const string FeatureHeader = "### ✨ Features and Improvements";
const string BugfixHeader = "### 🐞 Bugfixes";
const string OtherHeader = "### 📝 Other";

ILogger log = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "changelogger.log"))
    .CreateLogger();

IDeserializer deserializer = new DeserializerBuilder()
    .WithTypeConverter(new YamlStringEnumConverter(log))
    .Build();

// TODO: Use CommandLineParser
// TODO: Take output file path and name
// TODO: Take input header file path and name (?)
// TODO: Take debug log level
// TODO: Change Type into filename (change.bug.yaml, #11111.feature.yaml)
// TODO: Take debug log level
if(Args.Count != 1) {
    throw new ArgumentException($"Please provide the relative path to project's changelog root directory as only argument.");
}

string projectDirectory = Path.Combine(Directory.GetCurrentDirectory(), Args[0]);
if (!Directory.Exists(projectDirectory))
{
    throw new ArgumentException($"❌ {projectDirectory} does't exist.");
}
log.Information("⏱️ Assembling changelog.md for {projectDirectory} ...", projectDirectory);

string headerPath = Path.Combine(projectDirectory, "_header.md");
string header = File.Exists(headerPath)
    ? File.ReadAllText(headerPath) 
    : throw new ArgumentException($"❌ {headerPath} does't exist.");
log.Debug("✔️ Successfully parsed header file.");

StringBuilder changelogStringBuilder = new(header.Trim());

List<string> versionPaths = new();
foreach (string versionDirectory in Directory.GetDirectories(projectDirectory))
{
    var versionDir = versionDirectory.Split(Path.DirectorySeparatorChar);

    if(!versionDir[versionDir.Length - 1].StartsWith("_"))
    {
        versionPaths.Add(versionDirectory);
    }
}

versionPaths.Sort();
versionPaths.Reverse();

log.Debug("✔️ Found {x} versions.", versionPaths.Count);

foreach (string versionDirectory in versionPaths) {
    changelogStringBuilder.Append(BuildVersion(versionDirectory));
}

var changelogFile = Path.Combine(projectDirectory, "changelog.md");
File.WriteAllText(changelogFile, changelogStringBuilder.ToString());
log.Information("✔️ Successfully wrote {changelogFile}.", changelogFile);

private string BuildVersion(string versionDirectory) {
    string? version = Path.GetFileName(versionDirectory);
    StringBuilder sb = new();
    sb.AppendLine();
    sb.Append(VersionHeader).AppendLine(version);

    List<ChangeEntryFile> changeEntryFiles = new();
    foreach (string changeFile in Directory.GetFiles(versionDirectory, "*.yaml")) {
        string fileContent = File.ReadAllText(changeFile);
        changeEntryFiles.Add(new ChangeEntryFile( 
            Path.GetFileNameWithoutExtension(changeFile).Trim(),
            deserializer.Deserialize<ChangeEntryFileContent>(fileContent)));
    }

    IEnumerable<ChangeEntryFile> generalEntryFiles = changeEntryFiles.Where(entry
        => entry.Content.TaskType == ChangeEntryTaskType.General);
    if (generalEntryFiles.Count() >  0) {
        sb.Append(BuildChanges(ChangeEntryTaskType.General, generalEntryFiles));
    }

    IEnumerable<ChangeEntryFile> featureEntryFiles = changeEntryFiles.Where(entry
        => entry.Content.TaskType == ChangeEntryTaskType.Feature);
    if (featureEntryFiles.Count() >  0) {
        sb.Append(BuildChanges(ChangeEntryTaskType.Feature, featureEntryFiles));
    }

    IEnumerable<ChangeEntryFile> bugfixEntryFiles = changeEntryFiles.Where(entry
        => entry.Content.TaskType == ChangeEntryTaskType.Bug);
    if (bugfixEntryFiles.Count() >  0) {
        sb.Append(BuildChanges(ChangeEntryTaskType.Bug, bugfixEntryFiles));
    }

    IEnumerable<ChangeEntryFile> otherEntryFiles = changeEntryFiles.Where(entry 
        => entry.Content.TaskType != ChangeEntryTaskType.General 
            && entry.Content.TaskType != ChangeEntryTaskType.Feature 
            && entry.Content.TaskType != ChangeEntryTaskType.Bug);
    if (otherEntryFiles.Count() >  0) {
        sb.Append(BuildChanges(ChangeEntryTaskType.Other, otherEntryFiles));
    }

    log.Debug("✔️ Built {x} changes of version {y}.", changeEntryFiles.Count, versionDirectory);
    return sb.ToString();
}

private string BuildChanges(ChangeEntryTaskType section, IEnumerable<ChangeEntryFile> entries) {
    StringBuilder sb = new();
    sb.AppendLine();
    string header = section switch
    {
        ChangeEntryTaskType.General => GeneralHeader,
        ChangeEntryTaskType.Feature => FeatureHeader,
        ChangeEntryTaskType.Bug => BugfixHeader,
        _ => OtherHeader
    };
    sb.AppendLine(header);
    sb.AppendLine();

    uint changes = 0;
    foreach (ChangeEntryFile changeFile in entries) {
        if(!changeFile.Content.UseContentInsteadOfFileName
            || changeFile.Content.Content == null) {
            sb.Append("* ").AppendLine(changeFile.Name);
            changes = 1;
            log.Debug("ℹ️ Used the file name {file} as change.", changeFile.Name);
            continue;
        }

        foreach (string content in changeFile.Content.Content) {
            sb.Append("* ").AppendLine(content);
            changes++;
        }

        log.Debug("ℹ️ Used the file content of {file} as change(s).", changeFile.Name);
    }

    log.Debug("✔️ Built {x} changes into the {section} section.", changes, section);
    return sb.ToString();
}

internal record ChangeEntryFile(string Name, ChangeEntryFileContent Content);

internal class ChangeEntryFileContent
{
    public ChangeEntryTaskType TaskType { get; set; }
    public bool UseContentInsteadOfFileName { get; set; }
    public ICollection<string>? Content { get; set; }
}

internal enum ChangeEntryTaskType 
{ 
    [EnumMember(Value = @"general")]
    General,

    [EnumMember(Value = @"feature")]
    Feature,

    [EnumMember(Value = @"bug")]
    Bug,

    [EnumMember(Value = @"other")]
    Other 
}

public class YamlStringEnumConverter : IYamlTypeConverter
{
    private ILogger Logger { get; set; }
    public YamlStringEnumConverter(ILogger logger)
    {
        Logger = logger;
    }

    public bool Accepts(Type type) => type.IsEnum;

    public object? ReadYaml(IParser parser, Type type)
    {
        Scalar parsedEnum = parser.Consume<Scalar>();
        string value = parsedEnum.Value;
        string capitalizedValue = value.Substring(0, 1).ToUpper() + parsedEnum.Value.Substring(1);

        IList<string> enumValues = Enum.GetValues(type)
            .Cast<ChangeEntryTaskType>()
            .Select(v => v.ToString())
            .ToList();;
        if (!enumValues.Contains(capitalizedValue))
        {
            Logger.Warning("❗ TaskType {value} is invalid, assuming 'other'.", value);
            return ChangeEntryTaskType.Other;
        }

        return Enum.Parse(type, capitalizedValue);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        MemberInfo enumMember = type.GetMember(value!.ToString()!).First();
        string? yamlValue = enumMember?.GetCustomAttributes<YamlMemberAttribute>(true).Select(ema => ema.Alias).FirstOrDefault() ?? value.ToString();
        emitter.Emit(new Scalar(yamlValue!));
    }
}