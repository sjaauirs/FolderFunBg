#pragma once

#include "framework.h"

extern HMODULE g_module;
extern const CLSID CLSID_FolderFunBg;
extern const wchar_t* const kFolderFunBgClsidString;

class CIDispatch final : public IDispatch
{
public:
    CIDispatch();

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    STDMETHODIMP GetTypeInfoCount(UINT* count) override;
    STDMETHODIMP GetTypeInfo(UINT typeInfo, LCID lcid, ITypeInfo** typeInfoOut) override;
    STDMETHODIMP GetIDsOfNames(
        REFIID riid,
        LPOLESTR* names,
        UINT nameCount,
        LCID lcid,
        DISPID* dispIds) override;
    STDMETHODIMP Invoke(
        DISPID dispIdMember,
        REFIID riid,
        LCID lcid,
        WORD flags,
        DISPPARAMS* parameters,
        VARIANT* result,
        EXCEPINFO* exceptionInfo,
        UINT* argumentError) override;

private:
    ~CIDispatch() = default;

    std::atomic<long> m_ref{1};
    std::wstring m_lastPath;
};

class CObjectWithSite final : public IObjectWithSite
{
public:
    CObjectWithSite();
    ~CObjectWithSite();

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    STDMETHODIMP SetSite(IUnknown* site) override;
    STDMETHODIMP GetSite(REFIID riid, void** siteOut) override;

private:
    void ReleaseResources();

    std::atomic<long> m_ref{1};
    IWebBrowser2* m_web = nullptr;
    IConnectionPoint* m_connectionPoint = nullptr;
    DWORD m_cookie = 0;
};

class ClassFactory final : public IClassFactory
{
public:
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    STDMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** object) override;
    STDMETHODIMP LockServer(BOOL lock) override;

private:
    ~ClassFactory() = default;

    std::atomic<long> m_ref{1};
};
