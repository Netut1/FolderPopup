; ===== Настройки =====
#define AppName "Folder Popup"
#define AppVer  "1.0.4"
#define AppExe  "FolderPopup.exe"
; Ссылка на публичный репозиторий без слеша в конце, например https://github.com/ник/репозиторий
#define Repo    "https://github.com/Netut1/FolderPopup"

#if Repo == "https://github.com/USER/REPO"
  #error Укажи в Repo ссылку на свой GitHub-репозиторий
#endif

; Разрядность: по умолчанию x64. Для x86 передай /DArch=x86
#ifndef Arch
  #define Arch "x64"
#endif

; SHA-256 скачиваемого exe (необязательно). Пусто = без проверки. Передавай так: /DSha256=...
#ifndef Sha256
  #define Sha256 ""
#endif

; Откуда качается exe: релиз v<версия>, файл FolderPopup_x64.exe / FolderPopup_x86.exe
#define ExeUrl Repo + "/releases/download/v" + AppVer + "/FolderPopup_" + Arch + ".exe"

#if Arch == "x64"
  #define RegRoot   "HKLM64"
  #define DotNetDir "{commonpf64}\dotnet"
#else
  #define RegRoot   "HKLM32"
  #define DotNetDir "{commonpf32}\dotnet"
#endif

[Setup]
AppId={{25BE1A68-1FD7-4DEF-BB19-2B6C361E1AC8}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=Netut1
DefaultDirName={autopf}\FolderPopup
DisableDirPage=auto
DisableProgramGroupPage=yes
ShowLanguageDialog=yes
UsePreviousLanguage=yes
PrivilegesRequired=lowest
MinVersion=10.0.14393
OutputDir=Output
OutputBaseFilename=FolderPopup-Setup-{#AppVer}-{#Arch}
Compression=lzma2
SolidCompression=yes
SetupIconFile=C:\Users\rdani\source\repos\FolderPopup\FolderPopup\app.ico
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
#if Arch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.DotNetMissing=.NET 10 Desktop Runtime ({#Arch}) is required to run {#AppName}, but it was not found.%n%nOpen the download page? After installing .NET, click Next again.
russian.DotNetMissing=Для работы {#AppName} нужен .NET 10 Desktop Runtime ({#Arch}), но он не найден.%n%nОткрыть страницу загрузки? После установки .NET нажмите «Далее» ещё раз.
english.DownloadFailed=Could not download {#AppName} from GitHub. Check your internet connection and try again.
russian.DownloadFailed=Не удалось скачать {#AppName} с GitHub. Проверьте подключение к интернету и повторите попытку.

[Files]
; Файл скачивается на странице подготовки и копируется из временной папки
Source: "{tmp}\{#AppExe}"; DestDir: "{app}"; Flags: external ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--cleanup"; RunOnceId: "FolderPopupCleanup"; Flags: runhidden

[Code]
var
  DownloadPage: TDownloadWizardPage;

// Установлен ли .NET 10 Desktop Runtime нужной разрядности
function IsDotNet10Installed: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  FindRec: TFindRec;
begin
  Result := False;

  // Способ 1: реестр
  if RegGetValueNames({#RegRoot}, 'SOFTWARE\dotnet\Setup\InstalledVersions\{#Arch}\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 3) = '10.' then
      begin
        Result := True;
        Exit;
      end;

  // Способ 2: стандартная папка рантайма
  if FindFirst(ExpandConstant('{#DotNetDir}\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec) then
  begin
    FindClose(FindRec);
    Result := True;
  end;
end;

// Если .NET нет, предлагаем открыть страницу загрузки; шаг «Далее» блокируется
function CheckDotNet: Boolean;
var
  ErrCode: Integer;
begin
  Result := IsDotNet10Installed;
  if not Result then
    if MsgBox(CustomMessage('DotNetMissing'), mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0/runtime/desktop', '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;   // продолжать загрузку
end;
procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (CurPageID = wpSelectDir) or (CurPageID = wpReady) then
    if not CheckDotNet then
    begin
      Result := False;
      Exit;
    end;

  if CurPageID = wpReady then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('{#ExeUrl}', '{#AppExe}', '{#Sha256}');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        if not DownloadPage.AbortedByUser then
          SuppressibleMsgBox(CustomMessage('DownloadFailed') + #13#10#13#10 + GetExceptionMessage, mbCriticalError, MB_OK, IDOK);
        Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;