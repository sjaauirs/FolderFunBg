#include "ComBho.h"

#include "AppState.h"
#include "Hooks.h"
#include "Logging.h"
#include "PathUtils.h"

#include <new>

using namespace FolderFunBg;

std::atomic<long> g_objectCount{0};
std::atomic<long> g_lockCount{0};

const CLSID CLSID_FolderFunBg = {
    0x80bdc533,
    0xd192,
    0x445a,
    {0xa1, 0xd9, 0xf9, 0x13, 0x10, 0x02, 0x19, 0x19},
};

const wchar_t* const kFolderFunBgClsidString =
    L"{80BDC533-D192-445A-A1D9-F91310021919}";

namespace
{
    std::wstring NormalizeLocation(const std::wstring& value)
    {
        if (value.rfind(L"file:", 0) != 0)
        {
            return value;
        }

        DWORD length = MAX_PATH;
        wchar_t buffer[MAX_PATH] = {};
        if (SUCCEEDED(PathCreateFromUrlW(value.c_str(), buffer, &length, 0)))
        {
            return buffer;
        }

        return value;
    }

    bool WriteRegistryString(
        HKEY root,
        const wchar_t* subKey,
        const wchar_t* valueName,
        const wchar_t* value)
    {
        HKEY key = nullptr;
        if (RegCreateKeyExW(
                root,
                subKey,
                0,
                nullptr,
                0,
                KEY_WRITE,
                nullptr,
                &key,
                nullptr) != ERROR_SUCCESS)
        {
            return false;
        }

        const auto bytes = static_cast<DWORD>((wcslen(value) + 1) * sizeof(wchar_t));
        const auto status = RegSetValueExW(
            key,
            valueName,
            0,
            REG_SZ,
            reinterpret_cast<const BYTE*>(value),
            bytes);
        RegCloseKey(key);
        return status == ERROR_SUCCESS;
    }

    bool WriteRegistryDword(
        HKEY root,
        const wchar_t* subKey,
        const wchar_t* valueName,
        DWORD value)
    {
        HKEY key = nullptr;
        if (RegCreateKeyExW(
                root,
                subKey,
                0,
                nullptr,
                0,
                KEY_WRITE,
                nullptr,
                &key,
                nullptr) != ERROR_SUCCESS)
        {
            return false;
        }

        const auto status = RegSetValueExW(
            key,
            valueName,
            0,
            REG_DWORD,
            reinterpret_cast<const BYTE*>(&value),
            sizeof(value));
        RegCloseKey(key);
        return status == ERROR_SUCCESS;
    }
}

CIDispatch::CIDispatch()
{
    g_objectCount.fetch_add(1);
}

STDMETHODIMP CIDispatch::QueryInterface(REFIID riid, void** ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }

    if (riid == IID_IUnknown || riid == IID_IDispatch || riid == DIID_DWebBrowserEvents2)
    {
        *ppv = static_cast<IDispatch*>(this);
        AddRef();
        return S_OK;
    }

    *ppv = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CIDispatch::AddRef()
{
    g_objectCount.fetch_add(1);
    return static_cast<ULONG>(++m_ref);
}

ULONG STDMETHODCALLTYPE CIDispatch::Release()
{
    const long ref = --m_ref;
    g_objectCount.fetch_sub(1);
    if (ref == 0)
    {
        delete this;
        return 0;
    }

    return static_cast<ULONG>(ref);
}

STDMETHODIMP CIDispatch::GetTypeInfoCount(UINT* count)
{
    if (count != nullptr)
    {
        *count = 0;
    }
    return E_NOTIMPL;
}

STDMETHODIMP CIDispatch::GetTypeInfo(UINT, LCID, ITypeInfo**)
{
    return E_NOTIMPL;
}

STDMETHODIMP CIDispatch::GetIDsOfNames(REFIID, LPOLESTR*, UINT, LCID, DISPID*)
{
    return E_NOTIMPL;
}

STDMETHODIMP CIDispatch::Invoke(
    DISPID dispIdMember,
    REFIID,
    LCID,
    WORD,
    DISPPARAMS* parameters,
    VARIANT*,
    EXCEPINFO*,
    UINT*)
{
    if (dispIdMember != DISPID_DOCUMENTCOMPLETE || parameters == nullptr || parameters->cArgs < 2)
    {
        return S_OK;
    }

    VARIANT* url = &parameters->rgvarg[0];
    std::wstring location;

    if (url->vt == VT_BSTR && url->bstrVal != nullptr)
    {
        location = url->bstrVal;
    }
    else if (url->vt == (VT_BYREF | VT_BSTR) && url->pbstrVal != nullptr && *url->pbstrVal != nullptr)
    {
        location = *url->pbstrVal;
    }
    else if (url->vt == (VT_BYREF | VT_VARIANT) && url->pvarVal != nullptr &&
             url->pvarVal->vt == VT_BSTR && url->pvarVal->bstrVal != nullptr)
    {
        location = url->pvarVal->bstrVal;
    }

    if (location.empty())
    {
        return S_OK;
    }

    location = NormalizeLocation(location);
    if (location == m_lastPath)
    {
        return S_OK;
    }

    m_lastPath = location;
    AppState::Instance().EnsureHooks();
    Hooks::SetLocationForThread(GetCurrentThreadId(), location);
    return S_OK;
}

CObjectWithSite::CObjectWithSite()
{
    g_objectCount.fetch_add(1);
    AppState::Instance().Start();
}

CObjectWithSite::~CObjectWithSite()
{
    ReleaseResources();
}

STDMETHODIMP CObjectWithSite::QueryInterface(REFIID riid, void** ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }

    if (riid == IID_IUnknown || riid == IID_IObjectWithSite)
    {
        *ppv = static_cast<IObjectWithSite*>(this);
        AddRef();
        return S_OK;
    }

    *ppv = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CObjectWithSite::AddRef()
{
    g_objectCount.fetch_add(1);
    return static_cast<ULONG>(++m_ref);
}

ULONG STDMETHODCALLTYPE CObjectWithSite::Release()
{
    const long ref = --m_ref;
    g_objectCount.fetch_sub(1);
    if (ref == 0)
    {
        delete this;
        return 0;
    }

    return static_cast<ULONG>(ref);
}

STDMETHODIMP CObjectWithSite::SetSite(IUnknown* site)
{
    ReleaseResources();

    if (site == nullptr)
    {
        return S_OK;
    }

    HRESULT status = site->QueryInterface(
        IID_IWebBrowser2,
        reinterpret_cast<void**>(&m_web));
    if (FAILED(status) || m_web == nullptr)
    {
        ReleaseResources();
        return status;
    }

    IConnectionPointContainer* container = nullptr;
    status = m_web->QueryInterface(
        IID_IConnectionPointContainer,
        reinterpret_cast<void**>(&container));
    if (FAILED(status) || container == nullptr)
    {
        ReleaseResources();
        return status;
    }

    status = container->FindConnectionPoint(DIID_DWebBrowserEvents2, &m_connectionPoint);
    container->Release();
    if (FAILED(status) || m_connectionPoint == nullptr)
    {
        ReleaseResources();
        return status;
    }

    auto* dispatch = new (std::nothrow) CIDispatch();
    if (dispatch == nullptr)
    {
        ReleaseResources();
        return E_OUTOFMEMORY;
    }

    status = m_connectionPoint->Advise(dispatch, &m_cookie);
    dispatch->Release();
    if (FAILED(status))
    {
        ReleaseResources();
        return status;
    }

    return S_OK;
}

STDMETHODIMP CObjectWithSite::GetSite(REFIID, void** siteOut)
{
    if (siteOut != nullptr)
    {
        *siteOut = nullptr;
    }
    return E_NOTIMPL;
}

void CObjectWithSite::ReleaseResources()
{
    if (m_connectionPoint != nullptr)
    {
        if (m_cookie != 0)
        {
            m_connectionPoint->Unadvise(m_cookie);
            m_cookie = 0;
        }
        m_connectionPoint->Release();
        m_connectionPoint = nullptr;
    }

    if (m_web != nullptr)
    {
        m_web->Release();
        m_web = nullptr;
    }
}

STDMETHODIMP ClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppv = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }

    *ppv = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE ClassFactory::AddRef()
{
    g_lockCount.fetch_add(1);
    return static_cast<ULONG>(++m_ref);
}

ULONG STDMETHODCALLTYPE ClassFactory::Release()
{
    const long ref = --m_ref;
    g_lockCount.fetch_sub(1);
    if (ref == 0)
    {
        delete this;
        return 0;
    }

    return static_cast<ULONG>(ref);
}

STDMETHODIMP ClassFactory::CreateInstance(IUnknown* outer, REFIID riid, void** object)
{
    if (object == nullptr)
    {
        return E_POINTER;
    }

    *object = nullptr;
    if (outer != nullptr)
    {
        return CLASS_E_NOAGGREGATION;
    }

    auto* instance = new (std::nothrow) CObjectWithSite();
    if (instance == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    const HRESULT status = instance->QueryInterface(riid, object);
    instance->Release();
    return status;
}

STDMETHODIMP ClassFactory::LockServer(BOOL lock)
{
    if (lock)
    {
        g_lockCount.fetch_add(1);
    }
    else
    {
        g_lockCount.fetch_sub(1);
    }
    return S_OK;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, LPVOID* ppv)
{
    if (ppv == nullptr)
    {
        return E_POINTER;
    }

    *ppv = nullptr;
    if (!IsEqualCLSID(clsid, CLSID_FolderFunBg))
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    static ClassFactory factory;
    return factory.QueryInterface(riid, ppv);
}

STDAPI DllCanUnloadNow()
{
    return (g_objectCount.load() == 0 && g_lockCount.load() == 0) ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    const auto dllPath = PathUtils::GetModulePath(g_module);
    if (dllPath.empty())
    {
        return SELFREG_E_CLASS;
    }

    const std::wstring clsidKey =
        std::wstring(L"CLSID\\") + kFolderFunBgClsidString;
    const std::wstring inprocKey = clsidKey + L"\\InProcServer32";
    const std::wstring bhoKey =
        std::wstring(L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Browser Helper Objects\\") +
        kFolderFunBgClsidString;

    if (!WriteRegistryString(HKEY_CLASSES_ROOT, clsidKey.c_str(), nullptr, L"FolderFunBg") ||
        !WriteRegistryString(HKEY_CLASSES_ROOT, inprocKey.c_str(), nullptr, dllPath.c_str()) ||
        !WriteRegistryString(HKEY_CLASSES_ROOT, inprocKey.c_str(), L"ThreadingModel", L"Apartment") ||
        !WriteRegistryDword(HKEY_LOCAL_MACHINE, bhoKey.c_str(), L"NoInternetExplorer", 1))
    {
        return SELFREG_E_CLASS;
    }

    return S_OK;
}

STDAPI DllUnregisterServer()
{
    const std::wstring clsidKey =
        std::wstring(L"CLSID\\") + kFolderFunBgClsidString;
    const std::wstring bhoKey =
        std::wstring(L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Browser Helper Objects\\") +
        kFolderFunBgClsidString;

    RegDeleteTreeW(HKEY_LOCAL_MACHINE, bhoKey.c_str());
    RegDeleteTreeW(HKEY_CLASSES_ROOT, clsidKey.c_str());
    return S_OK;
}
