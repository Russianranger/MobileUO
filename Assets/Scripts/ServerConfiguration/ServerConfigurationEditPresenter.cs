using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ClassicUO.Data;
using DG.Tweening;
using Rssdp;
using UnityEngine;
using UnityEngine.UI;

public class ServerConfigurationEditPresenter : MonoBehaviour
{
    private ServerConfiguration serverConfigurationToEdit;
    public ServerConfiguration ServerConfigurationToEdit
    {
        get => serverConfigurationToEdit;
        set
        {
            serverConfigurationToEdit = value;
            UpdateInputFields();
        }
    }

    [SerializeField]
    private InputField serverNameInputField;
    
    [SerializeField]
    private InputField uoServerUrlInputField;
    
    [SerializeField]
    private InputField uoServerPortInputField;
    
    [SerializeField]
    private InputField fileDownloadServerUrlInputField;
    
    [SerializeField]
    private InputField fileDownloadServerPortInputField;
    
    [SerializeField]
    private InputField clientVersionInputField;

    [SerializeField]
    private Toggle useEncryptionToggle;
    
    [SerializeField]
    private Toggle useExternalStorageToggle;
    
    [SerializeField]
    private GameObject useExternalStorageParent;
    
    [SerializeField]
    private InputField clientPathForUnityEditorInputField;
    
    [SerializeField]
    private GameObject clientPathForUnityEditorParent;

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Transform saveButtonTransform;
     
    [SerializeField]
    private Button cancelButton;
    
    [SerializeField]
    private Button deleteServerConfigurationButton;
    
    [SerializeField]
    private Text deleteServerConfigurationButtonText;
    
    [SerializeField]
    private Button deleteServerFilesButton;

    [SerializeField]
    private Text deleteServerFilesButtonText;

    [SerializeField]
    private Text importCharacterProfilesFilesButtonText;

    [SerializeField]
    private Button importCharacterProfilesFilesButton;

    [SerializeField]
    private Button exportCharacterProfilesFilesButton;
    
    [SerializeField]
    private Button markFilesAsDownloadedButton;

    [SerializeField]
    private GameObject documentationButtonParent;

    [SerializeField]
    private Text validationErrorText;
    
    [SerializeField]
    private GameObject validationErrorTextParent;

    [SerializeField]
    private Button discoverButton;

    [SerializeField]
    private ServerDiscoveryButtonPresenter serverDiscoveryButtonPresenter;

    [SerializeField]
    private GameObject[] gameObjectToDisableForSupportedServers;

    public Action OnConfigurationEditSaved;
    public Action OnConfigurationEditCanceled;
    public Action OnConfigurationDeleted;
    public Action OnConfigurationFilesDeleted;

    private int deleteServerConfigurationButtonClickCount;
    private float deleteServerConfigurationButtonClickTime;
    private string deleteServerConfigurationButtonOriginalText;
    
    private int deleteServerFilesButtonClickCount;
    private float deleteServerFilesButtonClickTime;
    private string deleteServerFilesButtonOriginalText;

    private Coroutine showErrorCoroutine;
    private Vector3 saveButtonOriginalLocalPosition;

    private const string deleteButtonConfirmText = "Click again to Delete!";
    private const float deleteButtonRevertDuration = 2f;

    private const string importCharacterProfilesFilesButtonSuccessText = "Successfully imported!";
    private const float importCharacterProfilesFilesButtonSuccessTextDuration = 5f;
    private int importCharacterProfilesFilesButtonClickCount;
    private float importCharacterProfilesFilesButtonClickTime;
    private string importCharacterProfilesFilesButtonOriginalText;
    private Color importCharacterProfilesFilesButtonOriginalColor;
    private Color successColor = new Color(0.5897633f, 0.8490566f, 0.5727127f, 1f);

    public bool CreateServer;

    private string pendingClientImport;
    private bool importInProgress;
    private bool saving;
    private int importGeneration;
    private Button importClientFolderButton;
    private GameObject importPanel;

    private void Awake()
    {
        // Reuse the existing anchored transfer button; its parent is not a layout group.
        if (SupportsFolderImport)
        {
            importClientFolderButton = markFilesAsDownloadedButton;
            importClientFolderButton.GetComponentInChildren<Text>().text = "Import client folder";
        }
    }

    private static bool SupportsFolderImport => Application.platform == RuntimePlatform.Android || Application.isEditor;

    private async void OnImportClientFolderClicked()
    {
        if (importInProgress || saving) return;
        importInProgress = true;
        int generation = ++importGeneration;
        importPanel = MobileSettingsUI.Panel("Import client folder", out var content, AndroidClientFolderImport.Cancel);
        var status = MobileSettingsUI.Label(content, "Choose the folder containing your client assets.");
        MobileSettingsUI.Label(content, "Your existing client stays available until you save this configuration. Character profiles are preserved.");
        try
        {
            var importTarget = new ServerConfiguration { Name = "client", PreferExternalStorage = useExternalStorageToggle.isOn };
            string path = await AndroidClientFolderImport.Pick(Path.GetDirectoryName(importTarget.GetPathToSaveFiles()), progress =>
            {
                if (status != null)
                    status.text = progress.message + (progress.total > 0
                        ? $"\n{progress.copied / 1048576d:F0} / {progress.total / 1048576d:F0} MB" : "");
            });
            if (!isActiveAndEnabled || generation != importGeneration)
            {
                if (path != null) await Task.Run(() => Directory.Delete(path, true));
                return;
            }
            if (path != null)
            {
                DiscardPendingImport();
                pendingClientImport = path;
                importClientFolderButton.GetComponentInChildren<Text>().text = "Client ready — Save";
            }
        }
        catch (Exception e) { Debug.LogException(e); if (isActiveAndEnabled) ShowError(e.Message); }
        finally
        {
            importInProgress = false;
            if (importPanel != null) Destroy(importPanel);
        }
    }

    private void DiscardPendingImport()
    {
        if (pendingClientImport == null) return;
        string path = pendingClientImport;
        pendingClientImport = null;
        // Temp cleanup must not block the UI or turn a successful installation into a failure.
        Task.Run(() => { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { } });
    }

    
    private void UpdateInputFields()
    {
        if(CreateServer)
        {
            // don't show import/export/delete buttons when creating a new server configuration
            importCharacterProfilesFilesButton.gameObject.SetActive(false);
            exportCharacterProfilesFilesButton.gameObject.SetActive(false);
            deleteServerConfigurationButton.gameObject.SetActive(false);
            deleteServerFilesButton.gameObject.SetActive(false);
            markFilesAsDownloadedButton.gameObject.SetActive(SupportsFolderImport);
        }
        else
        {
            importCharacterProfilesFilesButton.gameObject.SetActive(true);
            exportCharacterProfilesFilesButton.gameObject.SetActive(true);
            deleteServerConfigurationButton.gameObject.SetActive(true);
            deleteServerFilesButton.gameObject.SetActive(true);
            markFilesAsDownloadedButton.gameObject.SetActive(true);
        }

        serverNameInputField.text = serverConfigurationToEdit?.Name ?? "";
        uoServerUrlInputField.text = serverConfigurationToEdit?.UoServerUrl ?? "";
        uoServerPortInputField.text = serverConfigurationToEdit?.UoServerPort ?? "2593";
        fileDownloadServerUrlInputField.text = serverConfigurationToEdit?.FileDownloadServerUrl ?? "";
        fileDownloadServerPortInputField.text = serverConfigurationToEdit?.FileDownloadServerPort ?? DownloadState.DefaultFileDownloadPort;
        clientVersionInputField.text = serverConfigurationToEdit?.ClientVersion ?? "";
        useEncryptionToggle.isOn = serverConfigurationToEdit?.UseEncryption ?? false;
        useExternalStorageToggle.isOn = serverConfigurationToEdit?.PreferExternalStorage ?? false;
        clientPathForUnityEditorInputField.text = serverConfigurationToEdit?.ClientPathForUnityEditor ?? "";
        clientPathForUnityEditorParent.SetActive(Application.isMobilePlatform == false);

        //foreach (var go in gameObjectToDisableForSupportedServers)
        //{
        //    go.SetActive(serverConfigurationToEdit?.SupportedServer == false);
        //}
        
        documentationButtonParent.SetActive(true);
        validationErrorTextParent.SetActive(false);
        
        useExternalStorageParent.SetActive(Application.platform == RuntimePlatform.Android);
        
        deleteServerConfigurationButtonOriginalText = deleteServerConfigurationButtonText.text;
        deleteServerFilesButtonOriginalText = deleteServerFilesButtonText.text;
        importCharacterProfilesFilesButtonOriginalText = importCharacterProfilesFilesButtonText.text;
        importCharacterProfilesFilesButtonOriginalColor = importCharacterProfilesFilesButton.GetComponent<Image>().color;

        ResetDeleteServerConfigurationButton();
        ResetDeleteServerFilesButton();
        ResetImportCharacterProfilesFilesButton();
    }

    private void OnEnable()
    {
        saveButton.onClick.AddListener(OnSaveButtonClicked);
        cancelButton.onClick.AddListener(() => OnConfigurationEditCanceled?.Invoke());
        deleteServerConfigurationButton.onClick.AddListener(OnDeleteServerConfigurationButtonClicked);
        deleteServerFilesButton.onClick.AddListener(OnDeleteServerFilesButtonClicked);
        if (SupportsFolderImport) markFilesAsDownloadedButton.onClick.AddListener(OnImportClientFolderClicked);
        else markFilesAsDownloadedButton.onClick.AddListener(OnMarkFilesAsDownloadedButtonClicked);
        discoverButton.onClick.AddListener(SearchForDevices);
        exportCharacterProfilesFilesButton.onClick.AddListener(OnExportCharacterProfilesButtonClicked);
        importCharacterProfilesFilesButton.onClick.AddListener(OnImportCharacterProfilesButtonClicked);

        saveButtonOriginalLocalPosition = saveButtonTransform.localPosition;
    }

    private async void SearchForDevices()
    {
        serverDiscoveryButtonPresenter.Toggle(true);
        using (var deviceLocator = new SsdpDeviceLocator())
        {
            try
            {
                var foundDevices = await deviceLocator.SearchAsync("urn:UOFileServer:device:UOFileServer:1");
                //Check isActiveAndEnabled in case the Edit Presenter exited already
                if (isActiveAndEnabled)
                {
                    var device = foundDevices.FirstOrDefault();
                    if (device == null)
                    {
                        Debug.Log("UOFileServer device was not found.");
                    }
                    else
                    {
                        var ip = device.DescriptionLocation.Authority;
                        Debug.Log($"UOFileServer device found at {ip}");
                        
                        var responseHeaders = device.ResponseHeaders;
                        if (responseHeaders.TryGetValues("LoginServer", out var loginServerValues))
                        {
                            uoServerUrlInputField.text = loginServerValues.FirstOrDefault();
                        }

                        if (responseHeaders.TryGetValues("LoginPort", out var loginPortValues))
                        {
                            uoServerPortInputField.text = loginPortValues.FirstOrDefault();
                        }

                        if (responseHeaders.TryGetValues("ClientVersion", out var clientVersionValues))
                        {
                            clientVersionInputField.text = clientVersionValues.FirstOrDefault();
                        }
                    
                        fileDownloadServerUrlInputField.text = ip;
                        fileDownloadServerPortInputField.text = DownloadState.DefaultFileDownloadPort;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        serverDiscoveryButtonPresenter.Toggle(false);
    }

    private void OnDisable()
    {
        importGeneration++;
        if (importInProgress) AndroidClientFolderImport.Cancel();
        DiscardPendingImport();
        if (importClientFolderButton != null)
            importClientFolderButton.GetComponentInChildren<Text>().text = "Import client folder";
        saveButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
        deleteServerConfigurationButton.onClick.RemoveAllListeners();
        deleteServerFilesButton.onClick.RemoveAllListeners();
        markFilesAsDownloadedButton.onClick.RemoveAllListeners();
        exportCharacterProfilesFilesButton.onClick.RemoveAllListeners();
        importCharacterProfilesFilesButton.onClick.RemoveAllListeners();
        
        ResetDeleteServerConfigurationButton();
        ResetDeleteServerFilesButton();
        ResetImportCharacterProfilesFilesButton();
    }
    
    private async void OnSaveButtonClicked()
    {
        if (saving || importInProgress) return;
        var valid = ValidateFields(out var validationError);
        if (valid == false)
        {
            ShowError(validationError);
            return;
        }
        
        string existing = serverConfigurationToEdit.GetPathToSaveFiles();
        var target = new ServerConfiguration { Name = serverNameInputField.text, PreferExternalStorage = useExternalStorageToggle.isOn };
        string destination = target.GetPathToSaveFiles();
        if (destination != existing && Directory.Exists(destination))
        {
            ShowError("A client folder with this name already exists. Choose another name.");
            return;
        }
        saving = true;
        var savePanel = MobileSettingsUI.Panel("Saving configuration", out var saveContent, null);
        MobileSettingsUI.Label(saveContent, "Installing client files and preserving character profiles…");
        try
        {
            string staged = pendingClientImport;
            if (staged != null)
                await Task.Run(() => ClientImportTransaction.Install(staged, destination, existing, true));
            else if (destination != existing && Directory.Exists(existing))
                await Task.Run(() => ClientImportTransaction.Install(existing, destination, existing));
            else Directory.CreateDirectory(destination);

            ServerConfigurationToEdit.Name = target.Name;
            ServerConfigurationToEdit.UoServerUrl = uoServerUrlInputField.text;
            ServerConfigurationToEdit.UoServerPort = uoServerPortInputField.text;
            ServerConfigurationToEdit.FileDownloadServerUrl = fileDownloadServerUrlInputField.text;
            ServerConfigurationToEdit.FileDownloadServerPort = fileDownloadServerPortInputField.text;
            ServerConfigurationToEdit.ClientVersion = clientVersionInputField.text;
            ServerConfigurationToEdit.UseEncryption = useEncryptionToggle.isOn;
            ServerConfigurationToEdit.PreferExternalStorage = useExternalStorageToggle.isOn;
            ServerConfigurationToEdit.ClientPathForUnityEditor = clientPathForUnityEditorInputField.text;
            if (staged != null) ServerConfigurationToEdit.AllFilesDownloaded = true;
            DiscardPendingImport();
            
            OnConfigurationEditSaved?.Invoke();
            if (destination != existing && Directory.Exists(existing))
            {
                Task.Run(() => { try { Directory.Delete(existing, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
            }
        }
        catch (Exception e) { Debug.LogException(e); ShowError("Could not save client files: " + e.Message); }
        finally { saving = false; Destroy(savePanel); }
    }

    private void ShowError(string validationError)
    {
        if (showErrorCoroutine != null)
        {
            StopCoroutine(showErrorCoroutine);
        }
        
        documentationButtonParent.SetActive(false);
        
        validationErrorText.text = validationError;
        validationErrorTextParent.SetActive(true);

        saveButtonTransform.localPosition = saveButtonOriginalLocalPosition;
        DOTween.Kill(saveButtonTransform);
        saveButtonTransform.DOShakePosition(1f, 10f);

        showErrorCoroutine = StartCoroutine(HideValidationErrorText());
    }

    private IEnumerator HideValidationErrorText()
    {
        yield return new WaitForSeconds(4);
        documentationButtonParent.SetActive(true);
        validationErrorTextParent.SetActive(false);
        saveButtonTransform.localPosition = saveButtonOriginalLocalPosition;
    }

    private bool ValidateFields(out string validationError)
    {
        //Server Name validation
        var serverName = serverNameInputField.text;
        if (string.IsNullOrWhiteSpace(serverName))
        {
            validationError = "Server Name cannot be empty.";
            return false;
        }

        if (!ClientImportTransaction.ValidConfigurationName(serverName))
        {
            validationError = "Server Name contains illegal characters for filesystem path.";
            return false;
        }

        var configsWithSameName = ServerConfigurationModel.ServerConfigurations.Where(x => string.Equals(x.Name, serverName, StringComparison.OrdinalIgnoreCase)).ToList();
        configsWithSameName.Remove(serverConfigurationToEdit);
        if (configsWithSameName.Count > 0)
        {
            validationError = "Server Name cannot be the same as that of another configuration.";
            return false;
        }

        //UO Server Url validation
        var uoServerUrl = uoServerUrlInputField.text;
        if (string.IsNullOrWhiteSpace(uoServerUrl))
        {
            validationError = "UO Server Address cannot be empty.";
            return false;
        }

        //UO Server Port validation
        var uoServerPort = uoServerPortInputField.text;
        if (int.TryParse(uoServerPort, out var uoServerPortResult) == false)
        {
            validationError = "UO Server port is not valid.";
            return false;
        }

        if (uoServerPortResult < 0 || uoServerPortResult > 65535)
        {
            validationError = "UO Server port needs to be between 0 and 65535";
            return false;
        }

        //Skip file download server url and port validation if AllFilesDownloaded is set to true
        if (serverConfigurationToEdit.AllFilesDownloaded == false && pendingClientImport == null)
        {
            //File Download Server Url validation
            var fileDownloadServerUrl = fileDownloadServerUrlInputField.text;
            fileDownloadServerUrl = fileDownloadServerUrl.Replace("http://", "");
            fileDownloadServerUrl = fileDownloadServerUrl.Replace("https://", "");
            if (string.IsNullOrWhiteSpace(fileDownloadServerUrl))
            {
                validationError = "File Download Server Address cannot be empty.";
                return false;
            }

            try
            {
                var unused = new UriBuilder("http", fileDownloadServerUrl, -1, null).Uri;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                validationError = "File Download Server Address is not valid";
                return false;
            }

            //File Server Port validation
            var fileServerPort = fileDownloadServerPortInputField.text;
            if (int.TryParse(fileServerPort, out var fileServerPortResult) == false)
            {
                validationError = "File Download Server port is not valid.";
                return false;
            }

            if (fileServerPortResult < 0 || fileServerPortResult > 65535)
            {
                validationError = "File Download Server port needs to be between 0 and 65535";
                return false;
            }
        }

        //Client version validation
        var clientVersion = clientVersionInputField.text;
        if (ClientVersionHelper.IsClientVersionValid(clientVersion, out _) == false)
        {
            validationError = "Client Version is not valid";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private void OnDeleteServerConfigurationButtonClicked()
    {
        if (deleteServerConfigurationButtonClickCount == 0)
        {
            deleteServerConfigurationButtonText.text = deleteButtonConfirmText;
            deleteServerConfigurationButtonClickCount++;
            deleteServerConfigurationButtonClickTime = Time.time;
        }
        else
        {
            OnConfigurationDeleted?.Invoke();
            serverConfigurationToEdit = null;
            ResetDeleteServerConfigurationButton();
        }
    }
    
    private void OnDeleteServerFilesButtonClicked()
    {
        if (deleteServerFilesButtonClickCount == 0)
        {
            deleteServerFilesButtonText.text = deleteButtonConfirmText;
            deleteServerFilesButtonClickCount++;
            deleteServerFilesButtonClickTime = Time.time;
        }
        else
        {
            OnConfigurationFilesDeleted?.Invoke();
            ResetDeleteServerFilesButton();
        }
    }

    private void OnExportCharacterProfilesButtonClicked()
    {
        var dataFolderPath = $"{serverConfigurationToEdit.GetPathToSaveFiles()}/Data";
        var exportZipFileName = $"{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}-{serverConfigurationToEdit.Name}.zip";
        var exportZipFilePath = $"{serverConfigurationToEdit.GetPathToSaveFiles()}/{exportZipFileName}";
        Debug.Log($"Export zip file path: {exportZipFilePath}");
        ZipFile.CreateFromDirectory(dataFolderPath, exportZipFilePath);

        NativeFilePicker.ExportFile(exportZipFilePath, (callback) => { }) ;
    }

    private void OnImportCharacterProfilesButtonClicked()
    {
        var dataFolderPath = $"{serverConfigurationToEdit.GetPathToSaveFiles()}/Data";

        NativeFilePicker.PickFile((path) =>
        {
            if (path == null)
            {
                Debug.Log("Import character profiles canceled");
                return;
            }

            importCharacterProfilesFilesButtonText.text = importCharacterProfilesFilesButtonSuccessText;
            importCharacterProfilesFilesButton.GetComponent<Image>().color = successColor;
            importCharacterProfilesFilesButtonClickCount++;
            importCharacterProfilesFilesButtonClickTime = Time.time;

            Debug.Log($"path: {path}");
            ZipFile.ExtractToDirectory(path, dataFolderPath, true);
        }, new string[] { "application/zip", "public.zip-archive" });
    }
    
    private void OnMarkFilesAsDownloadedButtonClicked()
    {
        serverConfigurationToEdit.AllFilesDownloaded = true;
        ResetDeleteServerFilesButton();
    }

    private void Update()
    {
        if (deleteServerConfigurationButtonClickCount > 0 && Time.time > deleteServerConfigurationButtonClickTime + deleteButtonRevertDuration)
        {
            ResetDeleteServerConfigurationButton();
        }
        
        if (deleteServerFilesButtonClickCount > 0 && Time.time > deleteServerFilesButtonClickTime + deleteButtonRevertDuration)
        {
            ResetDeleteServerFilesButton();
        }

        if (importCharacterProfilesFilesButtonClickCount > 0 && Time.time > importCharacterProfilesFilesButtonClickTime + importCharacterProfilesFilesButtonSuccessTextDuration)
        {
            ResetImportCharacterProfilesFilesButton();
        }
    }

    private void ResetDeleteServerConfigurationButton()
    {
        deleteServerConfigurationButtonText.text = deleteServerConfigurationButtonOriginalText;
        deleteServerConfigurationButtonClickCount = 0;
    }

    private void ResetImportCharacterProfilesFilesButton()
    {
        importCharacterProfilesFilesButtonText.text = importCharacterProfilesFilesButtonOriginalText;
        importCharacterProfilesFilesButton.GetComponent<Image>().color = importCharacterProfilesFilesButtonOriginalColor;
        importCharacterProfilesFilesButtonClickCount = 0;
    }
    
    private void ResetDeleteServerFilesButton()
    {
        deleteServerFilesButtonText.text = deleteServerFilesButtonOriginalText;
        deleteServerFilesButtonClickCount = 0;
        if (ServerConfigurationToEdit != null && !CreateServer)
        {
            deleteServerFilesButton.gameObject.SetActive(ServerConfigurationToEdit.AllFilesDownloaded);
            markFilesAsDownloadedButton.gameObject.SetActive(deleteServerFilesButton.gameObject.activeSelf == false);
        }
    }
}
