; =============================================================================
;  Instalador de GenteFit (Producto 3 - FP.059)
;  Se compila con Inno Setup 6 o 7 (https://jrsoftware.org/isinfo.php).
;  No lo compiles a mano: ejecuta crear-instalador.cmd, que primero publica la
;  aplicación en la carpeta "publicacion" y después genera Salida\setup.exe.
;
;  Qué hace el instalador:
;    1. Comprueba que hay SQL Server en el equipo y deja elegir la instancia.
;    2. Pide el email y la contraseña del primer administrador.
;    3. Copia la aplicación (autocontenida: no necesita tener .NET instalado).
;    4. Escribe appsettings.Production.json con la conexión y el puerto.
;    5. Crea la base de datos y el administrador (GenteFit.web.exe --preparar-bd).
;    6. Crea los accesos directos y, si se quiere, abre GenteFit al terminar.
; =============================================================================

#define NombreApp     "GenteFit"
#define VersionApp    "1.0.0"
#define Editor        "Equipo GenteFit - UOC FP.059"
#define Ejecutable    "GenteFit.web.exe"
#define Puerto        "5190"

; Si la aplicación no está publicada, el setup.exe saldría sin GenteFit.web.exe y fallaría al instalar.
; Esta comprobación hace que la compilación se pare con un mensaje claro.
#if !FileExists(AddBackslash(SourcePath) + "publicacion\" + Ejecutable)
  #error Falta publicacion\GenteFit.web.exe: la aplicación no está publicada. No compiles este fichero desde Inno Setup; ejecuta crear-instalador.cmd.
#endif

[Setup]
AppId={{6B0E2F7C-5A1D-4C55-9F1E-2C7B8E0A4F59}
AppName={#NombreApp}
AppVersion={#VersionApp}
AppVerName={#NombreApp} {#VersionApp}
AppPublisher={#Editor}
DefaultDirName={autopf}\{#NombreApp}
DefaultGroupName={#NombreApp}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Salida
OutputBaseFilename=setup
SetupIconFile=..\GenteFit.web\wwwroot\favicon.ico
UninstallDisplayIcon={app}\{#Ejecutable}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
MinVersion=10.0

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "demo"; Description: "Cargar datos de demostración (usuarios de prueba, actividades, sesiones y reservas)"; GroupDescription: "Datos:"; Flags: unchecked

[Files]
Source: "publicacion\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\GenteFit.DemoXml\DatosPrueba\1_actividades_correcto.xml"; DestDir: "{commonappdata}\{#NombreApp}\xml"; DestName: "actividades.xml"; Flags: onlyifdoesntexist

[Dirs]
; Carpeta de datos con permiso de escritura para los usuarios (configuración del centro).
Name: "{commonappdata}\{#NombreApp}"; Permissions: users-modify

[Icons]
Name: "{group}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"; Parameters: "--abrir-navegador"; WorkingDir: "{app}"; Comment: "Abre GenteFit en el navegador"
Name: "{group}\Reparar la base de datos de {#NombreApp}"; Filename: "{app}\{#Ejecutable}"; Parameters: "--preparar-bd"; WorkingDir: "{app}"; Comment: "Crea o actualiza la base de datos"
Name: "{group}\Desinstalar {#NombreApp}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"; Parameters: "--abrir-navegador"; WorkingDir: "{app}"; Tasks: escritorio

[Run]
Filename: "{app}\{#Ejecutable}"; Parameters: "--abrir-navegador"; WorkingDir: "{app}"; Description: "Abrir {#NombreApp} ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Cierra la aplicación si está abierta antes de borrar sus ficheros.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#Ejecutable}"; Flags: runhidden; RunOnceId: "CerrarGenteFit"

[UninstallDelete]
Type: files; Name: "{app}\appsettings.Production.json"
Type: files; Name: "{app}\preparar-bd.log"

[Code]
var
  PaginaSql: TInputQueryWizardPage;
  PaginaAdmin: TInputQueryWizardPage;
  InstanciasSql: String;

{ ---------- SQL Server ---------- }

{ Lee del registro las instancias de SQL Server instaladas en el equipo. }
function BuscarInstanciasSql(): String;
var
  Nombres: TArrayOfString;
  I: Integer;
begin
  Result := '';
  if RegGetValueNames(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', Nombres) then
    for I := 0 to GetArrayLength(Nombres) - 1 do
    begin
      if Result <> '' then Result := Result + ', ';
      if CompareText(Nombres[I], 'MSSQLSERVER') = 0 then
        Result := Result + 'localhost'
      else
        Result := Result + 'localhost\' + Nombres[I];
    end;
end;

function ServidorPorDefecto(): String;
begin
  if Pos('localhost\SQLEXPRESS', InstanciasSql) > 0 then
    Result := 'localhost\SQLEXPRESS'
  else if InstanciasSql <> '' then
  begin
    Result := InstanciasSql;
    if Pos(',', Result) > 0 then
      Result := Copy(Result, 1, Pos(',', Result) - 1);
  end
  else
    Result := 'localhost\SQLEXPRESS';
end;

{ ---------- Validaciones ---------- }

function EmailValido(const Email: String): Boolean;
var
  Arroba: Integer;
begin
  Arroba := Pos('@', Email);
  Result := (Arroba > 1) and (Pos('.', Copy(Email, Arroba + 1, Length(Email))) > 1)
            and (Pos(' ', Email) = 0) and (Pos('"', Email) = 0);
end;

function TieneLetraYNumero(const Texto: String): Boolean;
var
  I: Integer;
  Letra, Numero: Boolean;
begin
  Letra := False;
  Numero := False;
  for I := 1 to Length(Texto) do
  begin
    if (Texto[I] >= '0') and (Texto[I] <= '9') then Numero := True
    else if ((Texto[I] >= 'a') and (Texto[I] <= 'z')) or ((Texto[I] >= 'A') and (Texto[I] <= 'Z')) then Letra := True;
  end;
  Result := Letra and Numero;
end;

{ Escapa un texto para escribirlo dentro de una cadena JSON. }
function Json(const Texto: String): String;
begin
  Result := Texto;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

{ ---------- Páginas del asistente ---------- }

procedure InitializeWizard();
var
  Descripcion: String;
begin
  InstanciasSql := BuscarInstanciasSql();

  if InstanciasSql <> '' then
    Descripcion := 'Instancias encontradas en este equipo: ' + InstanciasSql + '.'
  else
    Descripcion := 'ATENCIÓN: no se ha encontrado SQL Server en este equipo. Instala SQL Server Express ' +
                   '(gratuito) antes de usar GenteFit, o escribe el nombre de un servidor de la red.';

  PaginaSql := CreateInputQueryPage(wpSelectDir,
    'Base de datos', 'GenteFit guarda sus datos en Microsoft SQL Server.',
    Descripcion + #13#10#13#10 + 'Se usará la autenticación de Windows del usuario actual.');
  PaginaSql.Add('Servidor SQL Server:', False);
  PaginaSql.Add('Nombre de la base de datos:', False);
  PaginaSql.Values[0] := ServidorPorDefecto();
  PaginaSql.Values[1] := 'GenteFit';

  PaginaAdmin := CreateInputQueryPage(PaginaSql.ID,
    'Administrador', 'Cuenta del primer administrador de GenteFit.',
    'Con esta cuenta se entra en el panel de administración para crear el resto de usuarios. ' +
    'La contraseña debe tener al menos 8 caracteres, con letras y números.' + #13#10 +
    'Si ese email ya es un administrador de la base de datos, se le pone esta contraseña.');
  PaginaAdmin.Add('Email del administrador:', False);
  PaginaAdmin.Add('Contraseña:', True);
  PaginaAdmin.Add('Repite la contraseña:', True);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = PaginaSql.ID then
  begin
    if (Trim(PaginaSql.Values[0]) = '') or (Trim(PaginaSql.Values[1]) = '') then
    begin
      MsgBox('Indica el servidor y el nombre de la base de datos.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = PaginaAdmin.ID then
  begin
    if not EmailValido(Trim(PaginaAdmin.Values[0])) then
    begin
      MsgBox('El email del administrador no es válido.', mbError, MB_OK);
      Result := False;
    end
    else if (Length(PaginaAdmin.Values[1]) < 8) or not TieneLetraYNumero(PaginaAdmin.Values[1]) then
    begin
      MsgBox('La contraseña debe tener al menos 8 caracteres y combinar letras y números.', mbError, MB_OK);
      Result := False;
    end
    else if PaginaAdmin.Values[1] <> PaginaAdmin.Values[2] then
    begin
      MsgBox('Las contraseñas no coinciden.', mbError, MB_OK);
      Result := False;
    end
    else if (Pos('"', PaginaAdmin.Values[1]) > 0) or (Copy(PaginaAdmin.Values[1], Length(PaginaAdmin.Values[1]), 1) = '\') then
    begin
      MsgBox('La contraseña no puede contener comillas dobles (") ni acabar en barra invertida (\).', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine +
            'Base de datos:' + NewLine +
            Space + 'Servidor: ' + PaginaSql.Values[0] + NewLine +
            Space + 'Base de datos: ' + PaginaSql.Values[1] + NewLine + NewLine +
            'Administrador:' + NewLine + Space + Trim(PaginaAdmin.Values[0]) + NewLine + NewLine +
            'Dirección de la aplicación:' + NewLine + Space + 'http://localhost:{#Puerto}';
  if MemoTasksInfo <> '' then
    Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

{ ---------- Configuración y preparación de la base de datos ---------- }

procedure EscribirConfiguracion();
var
  Conexion, Contenido: String;
begin
  Conexion := 'Server=' + Trim(PaginaSql.Values[0]) + ';Database=' + Trim(PaginaSql.Values[1]) +
              ';Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True';

  Contenido :=
    '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "GenteFit": "' + Json(Conexion) + '"' + #13#10 +
    '  },' + #13#10 +
    '  "Urls": "http://localhost:{#Puerto}",' + #13#10 +
    '  "GenteFit": {' + #13#10 +
    '    "CarpetaDatos": "' + Json(ExpandConstant('{commonappdata}\{#NombreApp}')) + '"' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;

  SaveStringToFile(ExpandConstant('{app}\appsettings.Production.json'), Contenido, False);
end;

{ Deja la ruta del XML de ejemplo en la configuración del centro (solo si aún no hay configuración). }
procedure EscribirConfiguracionCentro();
var
  Fichero: String;
begin
  Fichero := ExpandConstant('{commonappdata}\{#NombreApp}\configuracion-centro.json');
  if not FileExists(Fichero) then
    SaveStringToFile(Fichero,
      '{' + #13#10 +
      '  "NombreCentro": "GenteFit",' + #13#10 +
      '  "RutaFuenteXml": "' + Json(ExpandConstant('{commonappdata}\{#NombreApp}\xml\actividades.xml')) + '"' + #13#10 +
      '}' + #13#10, False);
end;

procedure PrepararBaseDatos();
var
  Parametros, Log: String;
  Codigo: Integer;
begin
  Log := ExpandConstant('{app}\preparar-bd.log');
  Parametros := '--preparar-bd --sin-pausa --admin-email "' + Trim(PaginaAdmin.Values[0]) +
                '" --admin-contrasena "' + PaginaAdmin.Values[1] + '"';
  if WizardIsTaskSelected('demo') then
    Parametros := Parametros + ' --demo';

  WizardForm.StatusLabel.Caption := 'Creando la base de datos de GenteFit...';

  if not FileExists(ExpandConstant('{app}\{#Ejecutable}')) then
  begin
    MsgBox('Falta el programa ' + ExpandConstant('{app}\{#Ejecutable}') + '.' + #13#10#13#10 +
           'Este setup.exe se generó sin publicar la aplicación, o el antivirus ha bloqueado el fichero. ' +
           'Genera el instalador con crear-instalador.cmd y vuelve a instalar.', mbError, MB_OK);
    Exit;
  end;

  { GenteFit.web.exe guarda el resultado en preparar-bd.log (en la carpeta de instalación). }
  if not Exec(ExpandConstant('{app}\{#Ejecutable}'), Parametros, ExpandConstant('{app}'),
              SW_HIDE, ewWaitUntilTerminated, Codigo) then
  begin
    MsgBox('No se ha podido ejecutar ' + ExpandConstant('{app}\{#Ejecutable}') + ':' + #13#10 +
           SysErrorMessage(Codigo), mbError, MB_OK);
  end
  else if Codigo <> 0 then
  begin
    MsgBox('GenteFit se ha instalado, pero no se ha podido preparar la base de datos.' + #13#10#13#10 +
           'Comprueba que SQL Server está en marcha y que el servidor "' + PaginaSql.Values[0] + '" es correcto.' + #13#10 +
           'Encontrarás el detalle en:' + #13#10 + Log + #13#10#13#10 +
           'Cuando lo hayas resuelto, usa "Reparar la base de datos de GenteFit" en el menú Inicio ' +
           '(o vuelve a ejecutar este instalador para crear el administrador).',
           mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    EscribirConfiguracion();
    EscribirConfiguracionCentro();
    PrepararBaseDatos();
  end;
end;
