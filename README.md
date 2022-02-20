# Changelogger

## About

Changelogger is an attempt to help development teams with the issue that the usual pull request workflow often leads to some annoying last minute conflicts in some version line or changelog entry.

The underlying cause is: While your pull request was in process another pull requested was completed and now you need to merge the new main branch into your feature branch again and resolve the conflict by deciding upon the order.

Changelogger avoids those conflicts in changelog files completely by generating the changelog file from building blocks:

- one or more *project folder(s)* (for grouping and providing a header).
- a *header template*.
- one or more *version folder(s)* (for grouping) in a *project folder*.
- one or more *change file(s)* (to hold the actual information to a change).

After a one time setup, Changelogger then will able to generate a `changelog.md` file for and within the provided *project folder*.

The last piece to the solution is to let the CI-pipeline call Changelogger and commit the resulting file before the merge into the main branch happens.

## Setup

### Repository

1. Put the `changelogger.csx` file somewhere inside your repository.
1. Create a folder inside your repository (e.g. `./changelog` or `./docs/project-a-changelog`)
1. Put a `_header.md` file and an `Unreleased` folder in this folder.
   - And another folder for each Version you already have (e.g. `1.0.0`).
   - If the folder starts with an `_` it will be ignored by Changelogger (e.g. `_OldChangelog`).
1. Put a `<change>.yaml` file into the according subfolder for each change you want to track.

For their format see the section below or take a look in the `templates` folder of this repository.
For an example of the structure you also can refer to the `example\project1` folder of this repository.

It should now look something like this:

```Text
|- project-root
|  |- ...
|  |- changelog
|  |  |- Unreleased
|  |  |  |- #20000.yaml
|  |  |- 1.0.0
|  |  |  |- #10000.yaml
|  |  |- _header.md
|  |- ...
|  |- changelogger.csx
|  |- ...
```

### dotnet-script

To run .NET scripts (csx files) one needs a somewhat current [.NET Runtime](https://dotnet.microsoft.com/en-us/download) installed.

Additionally one needs the according dotnet tool to be installed, the dotnet CLI can handle this:

`dotnet tool install -g dotnet-script`

## Local Usage

### Change tracking workflow

Each change you want to have appear in the generated `changelog.md` file is represented by a `.yaml` file having 2-4 relevant properties:

- The *filename* without the `.yaml` extension
- The file has 3 properties:
  - TaskType (either 'general', 'feature', 'bug' or 'other', default: 'other')
  - UseContentInsteadOfFileName (either true or false, default: false)
  - Content (a list)

In the simplest case that one only wants to reference a ticket (e.g. `#12345`) or note a short one-liner, naming the file `Simple change 0815.yaml` will be sufficient.

If you want to add more than one line added, open the file, change `UseContentInsteadOfFileName` to `true` and the `Content` list will be used instead (!) of the filename.

The `TaskType` is used to group the entries in this order:

- General
- Features and Improvements
- Bugfixes
- Other

### Generation

The script must be provided with the relative path to your *project folder*. For this repository this would look like:

```bash
ProjectPath> dotnet script changelog.csx example/project1
```

Changelogger's output should inform about what it does and you will find a newly generated `changelog.md` in the provided *project folder*.

## Continuous Integration

As generating the changelog file locally before committing would have change nothing regarding to the conflicts it's crucial to let the CI-Pipeline handle the generation.

1. The pipeline should make sure the [dotnet-script](#dotnet-script) is installed and (probably early to abort on errors) [generate](#Generation) the `changelog.md`.

    e.g. Azure Tasks:

    ```yaml
    - task: Bash@3
      inputs:
        targetType: 'inline'
        script: 'dotnet tool install -g dotnet-script'

    - task: DotNetCoreCLI@2
      inputs:
        command: 'custom'
        custom: 'script'
        arguments: 'changelogger.csx example/project1'
    ```

1. The pipeline should (towards the end) stage, commit and push (without triggering itself or another pipeline) the generated `changelog.md` file(s).

    e.g. Azure Tasks:

    ```yaml
    - task: Bash@3
      inputs:
        targetType: 'inline'
        script: |
          git config --global user.email "changelogger@example.com"
          git config --global user.name "Changelogger"
          git commit -a -m "Generated changelog.md [skip ci]"
          git push origin HEAD:$(Build.SourceBranch)
    ```
