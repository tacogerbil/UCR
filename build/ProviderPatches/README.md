# Provider source patches

`Apply-IOWrapperInputOrdering.ps1` restores the established stuck-input fix in the exact pinned IOWrapper source: physical subscription callbacks execute synchronously instead of being launched as independent scheduled tasks. This preserves press/release ordering for providers that use the shared device handler.

`Apply-CoreViGEmDs4Serialization.ps1` is applied to the exact pinned IOWrapper source before the provider build.

It changes only the ViGEm DS4 handler: axis, button, and D-pad report mutation/transmission are kept synchronous and serialized through a per-controller lock. This prevents concurrent DS4 report writes from interleaving while leaving the Xbox 360 handler unchanged.

The former pinned `Core_ViGEm.dll` override is intentionally not used, because it would overwrite the patched provider produced from source. Core Interception keeps its approved DLL override.

`Apply-CoreViGEmRemoveCostura.ps1` removes Costura.Fody weaving from Core_ViGEm's own build. It has no
native payload of its own to embed (that belongs to its `Nefarius.ViGEmClient` dependency, handled
separately below) — the Costura-generated module initializer only added a call to
`Costura.AssemblyLoader.Attach()` that never had anything to extract, and its cross-process `Mutex`
setup does not resolve under .NET 8's BCL (`MissingMethodException` on `Mutex.SetAccessControl`),
crashing the provider before `InitLibrary()` runs. See `build/ProviderOverrides/Providers/Core_ViGEm/`
for the matching fix to the precompiled `Nefarius.ViGEmClient.dll` dependency, which has the same
Costura-crash problem but *does* have a real native payload to extract.
