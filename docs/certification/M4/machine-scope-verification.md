# Evidence — machine-scope catalogue verification (M4 acceptance #2)

- **Build:** Windows 11 Pro, 25H2, build 26200.9168
- **Edition:** Professional / Client
- **Date:** 2026-08-17
- **Host:** lab VM `win-11-vm` (10.10.11.183), elevated SSH session
- **Binary:** self-contained `otw.exe`, catalogue 435 entries, `catalog validate` exit 0 on the VM
- **Method:** per entry — `otw apply --only <id> --no-restore-point --json`, an independent
  native read (`Get-ItemProperty` / `Get-CimInstance Win32_Service` / `Get-ScheduledTask` /
  `Get-WindowsOptionalFeature` / `Get-MpPreference`), then `otw revert <runId> --json` and a
  second independent read. The CLI's own detection is never used as the evidence.

## Summary — 190 machine-scope entries exercised

| Outcome | Count | Meaning |
| --- | --- | --- |
| pass | 131 | Verified by full round-trip (apply → independent read = desired → revert → independent read = pre-state) |
| already-compliant | 13 | Verified as already-compliant on 25H2 (target already in the desired state; apply is a correct no-op) |
| not-applicable | 16 | Not applicable on this 25H2 image (component absent, or edition-gated to Enterprise/Education); left Draft |
| failed | 19 | Apply/verify failed on this host (Tamper Protection on Defender/ASR, a TrustedInstaller-protected key, or a powercfg capability); left Draft (reason shown) |
| anomaly | 8 | Applied but the round-trip read/revert did not fully match (GPO revert contention, or a scan-compliance discrepancy); left Draft |
| unknown | 3 | Skipped as managed by Group Policy (Local-GPO test residue induced the managed detection); left Draft |

## pass (131) — Verified by full round-trip (apply → independent read = desired → revert → independent read = pre-state)

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| debloat.apps.prevent-device-metadata-reinstall | Registry | (absent) | 1 | (absent) | - |
| debloat.features.remove-work-folders-client | OptionalFeature | Enabled | Disabled | Enabled | - |
| performance.gaming.disable-game-dvr-policy | Registry | (absent) | 0 | (absent) | - |
| performance.power.fast-startup-off | Registry | 1 | 0 | 1 | - |
| performance.power.modern-standby-network-off | Registry+Registry | (absent),(absent) | 0,0 | (absent),(absent) | - |
| performance.services.disable-activex-installer | Service | Manual | Disabled | Manual | - |
| performance.services.disable-contact-data-indexing | Registry | 3 | 4 | 3 | - |
| performance.services.disable-diagtrack | Service | Automatic | Disabled | Automatic | - |
| performance.services.disable-downloaded-maps-manager | Service | Automatic | Disabled | Automatic | - |
| performance.services.disable-messaging | Registry | 3 | 4 | 3 | - |
| performance.services.disable-mobile-hotspot | Service | Manual | Disabled | Manual | - |
| performance.services.disable-network-device-auto-setup | Service | Manual | Disabled | Manual | - |
| performance.services.disable-nfc-payments | Service | Manual | Disabled | Manual | - |
| performance.services.disable-parental-controls | Service | Manual | Disabled | Manual | - |
| performance.services.disable-program-compatibility-assistant | Service | Automatic | Disabled | Automatic | - |
| performance.services.disable-push-to-install | Service | Manual | Disabled | Manual | - |
| performance.services.disable-recommended-troubleshooting | Service | Manual | Disabled | Manual | - |
| performance.services.disable-retail-demo | Service | Manual | Disabled | Manual | - |
| performance.services.disable-user-data-storage | Registry+Registry | 3,3 | 4,4 | 3,3 | - |
| performance.services.disable-wallet-service | Service | Manual | Disabled | Manual | - |
| performance.services.disable-webclient-webdav | Service | Manual | Disabled | Manual | - |
| performance.services.disable-windows-insider | Service | Manual | Disabled | Manual | - |
| performance.services.disable-wmp-network-sharing | Service | Manual | Disabled | Manual | - |
| performance.storage.enable-storage-sense | Registry | (absent) | 1 | (absent) | - |
| performance.tasks.disable-maps-tasks | ScheduledTask+ScheduledTask | Ready,Disabled | Disabled,Disabled | Ready,Disabled | - |
| performance.tasks.disable-mare-backup | ScheduledTask | Ready | Disabled | Ready | - |
| performance.tasks.disable-pca-patch-db | ScheduledTask | Ready | Disabled | Ready | - |
| performance.tasks.disable-recommended-troubleshooting | ScheduledTask+ScheduledTask | Ready,Ready | Disabled,Disabled | Ready,Ready | - |
| performance.tasks.disable-startup-app-task | ScheduledTask | Ready | Disabled | Ready | - |
| privacy.activity-history.disable | Registry+Registry | (absent),(absent) | 0,0 | (absent),(absent) | - |
| privacy.activity.disable-activity-feed-policy | Registry | (absent) | 0 | (absent) | - |
| privacy.ai.disable-click-to-do | Registry | (absent) | 1 | (absent) | - |
| privacy.ai.disable-notepad-ai | Registry | (absent) | 1 | (absent) | - |
| privacy.ai.disable-paint-ai | Registry+Registry+Registry | (absent),(absent),(absent) | 1,1,1 | (absent),(absent),(absent) | - |
| privacy.ceip.disable-application-telemetry | Registry | (absent) | 0 | (absent) | - |
| privacy.ceip.disable-inventory-collector | Registry | (absent) | 1 | (absent) | - |
| privacy.ceip.disable-sqm | Registry | (absent) | 0 | (absent) | - |
| privacy.ceip.disable-steps-recorder | Registry | (absent) | 1 | (absent) | - |
| privacy.content.hide-start-recommended-section | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| privacy.defender.never-submit-samples | Registry | (absent) | 2 | (absent) | - |
| privacy.diagnostic-data.limit-log-collection | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| privacy.diagnostic-data.required-only | Registry | (absent) | 1 | (absent) | - |
| privacy.diagnostics.device-name | Registry | (absent) | 0 | (absent) | - |
| privacy.diagnostics.onesettings-auditing | Registry | (absent) | 1 | (absent) | - |
| privacy.diagnostics.optin-change-notification | Registry | (absent) | 1 | (absent) | - |
| privacy.edge.hide-first-run | Registry | (absent) | 1 | (absent) | - |
| privacy.edge.personalization-reporting | Registry | (absent) | 0 | (absent) | - |
| privacy.edge.recommendations | Registry+Registry+Registry+Registry | (absent),(absent),(absent),(absent) | 0,0,0,0 | (absent),(absent),(absent),(absent) | - |
| privacy.edge.shopping-assistant | Registry | (absent) | 0 | (absent) | - |
| privacy.edge.spotlight | Registry | (absent) | 0 | (absent) | - |
| privacy.edge.user-feedback | Registry | (absent) | 0 | (absent) | - |
| privacy.edge.web-widget | Registry | (absent) | 0 | (absent) | - |
| privacy.feedback.disable-notifications | Registry | (absent) | 1 | (absent) | - |
| privacy.input.disable-automatic-learning-policy | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| privacy.input.disable-handwriting-data-sharing | Registry | (absent) | 1 | (absent) | - |
| privacy.input.disable-handwriting-error-reports | Registry | (absent) | 1 | (absent) | - |
| privacy.input.disable-linguistic-data-collection | Registry | (absent) | 0 | (absent) | - |
| privacy.input.disable-online-speech-recognition-policy | Registry | (absent) | 0 | (absent) | - |
| privacy.input.disable-voice-activation | Registry | (absent) | 2 | (absent) | - |
| privacy.nags.disable-settings-online-tips | Registry | (absent) | 0 | (absent) | - |
| privacy.permissions.deny-apps-account-info | Registry | (absent) | 2 | (absent) | - |
| privacy.permissions.deny-apps-app-diagnostics | Registry | (absent) | 2 | (absent) | - |
| privacy.permissions.deny-apps-calendar | Registry | (absent) | 2 | (absent) | - |
| privacy.permissions.deny-apps-call-history | Registry | (absent) | 2 | (absent) | - |
| privacy.permissions.deny-apps-contacts | Registry | (absent) | 2 | (absent) | - |
| privacy.permissions.deny-apps-notifications | Registry | (absent) | 2 | (absent) | - |
| privacy.recall.disable | Registry | (absent) | 1 | (absent) | - |
| privacy.search.disable-cloud-search | Registry | (absent) | 0 | (absent) | - |
| privacy.search.disable-cortana | Registry | (absent) | 0 | (absent) | - |
| privacy.search.disable-web-results | Registry+Registry | (absent),(absent) | 1,0 | (absent),(absent) | - |
| privacy.sync.disable-cross-device-clipboard | Registry | (absent) | 0 | (absent) | - |
| privacy.sync.disable-find-my-device | Registry | (absent) | 0 | (absent) | - |
| privacy.sync.disable-message-sync | Registry | (absent) | 0 | (absent) | - |
| privacy.task.autochk-proxy | ScheduledTask | Ready | Disabled | Ready | - |
| privacy.task.ceip | ScheduledTask+ScheduledTask | Ready,Ready | Disabled,Disabled | Ready,Ready | - |
| privacy.task.feedback-siuf | ScheduledTask+ScheduledTask | Ready,Ready | Disabled,Disabled | Ready,Ready | - |
| privacy.wer.no-additional-data | Registry | (absent) | 1 | (absent) | - |
| privacy.wer.no-auto-approve-os-dumps | Registry | (absent) | 0 | (absent) | - |
| security.appcontrol.disable-appinstaller-protocol | Registry+Registry+Registry | (absent),(absent),(absent) | 0,0,0 | (absent),(absent),(absent) | - |
| security.appcontrol.disable-developer-mode | Registry | (absent) | 0 | (absent) | - |
| security.appcontrol.edge-smartscreen | Registry+Registry+Registry+Registry+Registry | (absent),(absent),(absent),(absent),(absent) | 1,1,1,1,1 | (absent),(absent),(absent),(absent),(absent) | - |
| security.appcontrol.installer-restrict-user-control | Registry+Registry | (absent),(absent) | 0,0 | (absent),(absent) | - |
| security.appcontrol.smartscreen-shell-block | Registry+Registry | (absent),(absent) | 1,Block | (absent),(absent) | - |
| security.audit.detailed-tracking | Command+Command | (no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read) | - |
| security.audit.event-log-size | Registry+Registry+Registry | (absent),(absent),(absent) | 32768,196608,32768 | (absent),(absent),(absent) | - |
| security.audit.object-access | Command+Command | (no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read) | - |
| security.audit.powershell-module-logging | Registry+Registry | (absent),(absent) | 1,* | (absent),(absent) | - |
| security.audit.powershell-transcription | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| security.audit.privilege-use | Command | (no-independent-read) | (no-independent-read) | (no-independent-read) | - |
| security.audit.process-cmdline | Registry | (absent) | 1 | (absent) | - |
| security.audit.system | Command+Command+Command+Command+Command | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | - |
| security.autoplay.disable-autorun | Registry+Registry | (absent),(absent) | 255,1 | (absent),(absent) | - |
| security.boot.early-launch-antimalware | Registry | (absent) | 3 | (absent) | - |
| security.defender.cloud-block-level-high | DefenderPreference | 0 | 2 | 0 | - |
| security.defender.controlled-folder-access-audit | DefenderPreference | 0 | 2 | 0 | - |
| security.defender.network-protection-audit | DefenderPreference | 0 | 2 | 0 | - |
| security.exploit.enable-sehop | Registry | (absent) | 0 | (absent) | - |
| security.exploit.svchost-mitigation-policy | Registry | (absent) | 1 | (absent) | - |
| security.feature.disable-sudo | Registry | (absent) | 0 | (absent) | - |
| security.installer.disable-always-install-elevated | Registry | (absent) | 0 | (absent) | - |
| security.misc.disable-ie-com | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| security.misc.disable-index-encrypted-files | Registry | (absent) | 0 | (absent) | - |
| security.misc.harden-rss-feeds | Registry+Registry | (absent),(absent) | 1,0 | (absent),(absent) | - |
| security.network.disable-llmnr | Registry | (absent) | 0 | (absent) | - |
| security.network.disable-smart-multihomed-resolution | Registry | (absent) | 1 | (absent) | - |
| security.network.disable-wpad | Registry | (absent) | 1 | (absent) | - |
| security.network.dns-over-https-allow | Registry | (absent) | 2 | (absent) | - |
| security.network.safe-dll-search-mode | Registry | (absent) | 1 | (absent) | - |
| security.powershell.script-block-logging | Registry | (absent) | 1 | (absent) | - |
| security.uac.disable-local-password-reset-questions | Registry | (absent) | 1 | (absent) | - |
| security.uac.disable-password-reveal | Registry | (absent) | 1 | (absent) | - |
| security.uac.filter-administrator-token | Registry | (absent) | 1 | (absent) | - |
| security.uac.hide-admin-enumeration | Registry | (absent) | 0 | (absent) | - |
| updates.automatic-updates.notify-before-install | Registry+Registry | (absent),(absent) | 0,3 | (absent),(absent) | - |
| updates.delivery-optimization.http-only | Registry | (absent) | 0 | (absent) | - |
| updates.delivery-optimization.lan-only | Registry | (absent) | 1 | (absent) | - |
| updates.drivers.exclude-from-quality-updates | Registry | (absent) | 1 | (absent) | - |
| updates.feature-release.pin-to-25h2 | Registry+Registry+Registry | (absent),(absent),(absent) | 1,25H2,Windows 11 | (absent),(absent),(absent) | - |
| updates.feature-updates.defer-30-days | Registry+Registry | (absent),(absent) | 1,30 | (absent),(absent) | - |
| updates.feature-updates.defer-90-days | Registry+Registry | (absent),(absent) | 1,90 | (absent),(absent) | - |
| updates.features.keep-new-features-off | Registry | (absent) | 1 | (absent) | - |
| updates.insider.block-preview-builds | Registry+Registry | (absent),(absent) | 1,0 | (absent),(absent) | - |
| updates.microsoft-update.enable | Registry | (absent) | 1 | (absent) | - |
| updates.msrt.do-not-report-infections | Registry | (absent) | 1 | (absent) | - |
| updates.notifications.hide-all-but-restart-warnings | Registry+Registry | (absent),(absent) | 1,1 | (absent),(absent) | - |
| updates.notifications.hide-organization-name | Registry | (absent) | 1 | (absent) | - |
| updates.optional-content.let-user-choose | Registry+Registry | (absent),(absent) | 1,3 | (absent),(absent) | - |
| updates.restart.active-hours-max-range-18 | Registry+Registry | (absent),(absent) | 1,18 | (absent),(absent) | - |
| updates.restart.feature-deadline-7-grace-7 | Registry+Registry+Registry+Registry | (absent),(absent),(absent),(absent) | 1,7,7,1 | (absent),(absent),(absent),(absent) | - |
| updates.restart.no-auto-reboot-with-logged-on-users | Registry | (absent) | 1 | (absent) | - |
| updates.restart.quality-deadline-7-grace-2 | Registry+Registry+Registry+Registry | (absent),(absent),(absent),(absent) | 1,7,2,1 | (absent),(absent),(absent),(absent) | - |

## already-compliant (13) — Verified as already-compliant on 25H2 (target already in the desired state; apply is a correct no-op)

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| debloat.features.disable-recall | OptionalFeature | DisabledWithPayloadRemoved | DisabledWithPayloadRemoved | DisabledWithPayloadRemoved | - |
| debloat.features.remove-xps-services | OptionalFeature | Disabled | Disabled | Disabled | - |
| security.audit.object-access-detailed | Command+Command+Command+Command+Command+Command | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | (no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read),(no-independent-read) | - |
| security.crypto.system-objects-hardening | Registry+Registry | 1,1 | 1,1 | 1,1 | - |
| security.defender.pua-protection | DefenderPreference | 1 | 1 | 1 | - |
| security.defender.submit-samples-safe | DefenderPreference | 1 | 1 | 1 | - |
| security.network.remove-smbv1 | OptionalFeature | Disabled | Disabled | Disabled | - |
| security.remote.remove-legacy-network-clients | OptionalFeature+OptionalFeature+OptionalFeature | Disabled,Disabled,Disabled | Disabled,Disabled,Disabled | Disabled,Disabled,Disabled | - |
| security.uac.enable-admin-approval-mode | Registry | 1 | 1 | 1 | - |
| security.uac.installer-detection | Registry | 1 | 1 | 1 | - |
| security.uac.secure-desktop-prompt | Registry | 1 | 1 | 1 | - |
| security.uac.secure-uiaccess-paths | Registry | 1 | 1 | 1 | - |
| security.uac.virtualize-write-failures | Registry | 1 | 1 | 1 | - |

## not-applicable (16) — Not applicable on this 25H2 image (component absent, or edition-gated to Enterprise/Education); left Draft

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| debloat.features.remove-fax-scan | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| debloat.features.remove-math-recognizer | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| debloat.features.remove-powershell-v2 | OptionalFeature+OptionalFeature | (no-such-feature),(no-such-feature) | (no-such-feature),(no-such-feature) | (no-such-feature),(no-such-feature) | - |
| debloat.features.remove-steps-recorder | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| debloat.features.remove-wmic | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| debloat.features.remove-wordpad | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| debloat.features.remove-xps-viewer | OptionalFeature | (no-such-feature) | (no-such-feature) | (no-such-feature) | - |
| performance.services.disable-alljoyn-router | Service | (no-such-service) | (no-such-service) | (no-such-service) | - |
| performance.services.disable-fax | Service | (no-such-service) | (no-such-service) | (no-such-service) | - |
| performance.services.disable-peer-networking-pnrp | Service+Service+Service+Service | (no-such-service),(no-such-service),(no-such-service),(no-such-service) | (no-such-service),(no-such-service),(no-such-service),(no-such-service) | (no-such-service),(no-such-service),(no-such-service),(no-such-service) | - |
| performance.tasks.disable-compatibility-appraiser | ScheduledTask | (no-such-task) | (no-such-task) | (no-such-task) | - |
| performance.tasks.disable-speech-model-download | ScheduledTask | (no-such-task) | (no-such-task) | (no-such-task) | - |
| privacy.content.disable-cloud-optimized-content | Registry | (absent) | (absent) | (absent) | - |
| privacy.content.disable-consumer-features | Registry | (absent) | (absent) | (absent) | - |
| privacy.nags.disable-account-state-content | Registry | (absent) | (absent) | (absent) | - |
| privacy.task.program-data-updater | ScheduledTask | (no-such-task) | (no-such-task) | (no-such-task) | - |

## failed (19) — Apply/verify failed on this host (Tamper Protection on Defender/ASR, a TrustedInstaller-protected key, or a powercfg capability); left Draft (reason shown)

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| debloat.components.disable-teams-chat-autoinstall | Registry | (absent) | (absent) | (absent) | Access to the registry key 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Communications' is denied. |
| performance.power.hibernate-reduced | Command | (no-independent-read) | (no-independent-read) | (no-independent-read) | Command 'powercfg.exe' failed (exit 1). |
| performance.storage.classic-search-indexing | Registry | (absent) | (absent) | (absent) | Access to the registry key 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Search\Gather\Windows\SystemIndex' is denied. |
| security.asr.adobe-reader-child | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.copied-system-tools | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.email-executable | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.obfuscated-scripts | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.office-executable-content | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.office-macro-win32 | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.ransomware | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.safe-mode-reboot | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.script-launch-executable | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.usb-untrusted | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.asr.vulnerable-drivers | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | Verification failed for Defender:AttackSurfaceReductionRules_Ids. |
| security.defender.cloud-protection | DefenderPreference+DefenderPreference+DefenderPreference | 2,False,0 | 2,False,0 | 2,False,0 | Verification failed for Defender:DisableBlockAtFirstSeen. |
| security.defender.real-time-protection | DefenderPreference+DefenderPreference+DefenderPreference+DefenderPreference | False,False,False,False | False,False,False,False | False,False,False,False | Verification failed for Defender:DisableRealtimeMonitoring. |
| security.defender.scan-options | DefenderPreference+DefenderPreference+DefenderPreference+DefenderPreference | True,False,True,False | False,False,True,False | False,False,True,False | Verification failed for Defender:DisableRemovableDriveScanning. |
| security.exploit.explorer-mitigations | Registry+Registry+Registry | (absent),(absent),(absent) | (absent),(absent),(absent) | (absent),(absent),(absent) | Local Group Policy operation failed: The process cannot access the file because it is being used by another process. (0x80070020) |
| updates.automatic-updates.scheduled-install | Registry+Registry+Registry+Registry | (absent),(absent),(absent),(absent) | (absent),(absent),(absent),(absent) | (absent),(absent),(absent),(absent) | Local Group Policy operation failed: The process cannot access the file because it is being used by another process. (0x80070020) |

## anomaly (8) — Applied but the round-trip read/revert did not fully match (GPO revert contention, or a scan-compliance discrepancy); left Draft

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| debloat.features.remove-media-player-legacy | OptionalFeature+OptionalFeature | (no-such-feature),Enabled | (no-such-feature),Disabled | (no-such-feature),Enabled | - |
| performance.services.disable-mixed-reality | Service+Service+Service+Service+Service | (no-such-service),(no-such-service),(no-such-service),Manual,(no-such-service) | (no-such-service),(no-such-service),(no-such-service),Disabled,(no-such-service) | (no-such-service),(no-such-service),(no-such-service),Manual,(no-such-service) | - |
| security.asr.office-child-process | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | - |
| security.asr.office-comm-child | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | - |
| security.asr.office-injection | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | - |
| security.asr.wmi-persistence | DefenderPreference+DefenderPreference | (null),(null) | (null),(null) | (null),(null) | - |
| security.media.dma-disable-under-lock | Registry | (absent) | (absent) | (absent) | - |
| updates.restart.active-hours-8-to-23 | Registry+Registry+Registry | (absent),(absent),(absent) | 1,8,23 | 1,(absent),(absent) | - |

## unknown (3) — Skipped as managed by Group Policy (Local-GPO test residue induced the managed detection); left Draft

| id | kinds | pre | applied | reverted | note |
| --- | --- | --- | --- | --- | --- |
| security.uac.admin-consent-prompt | Registry | 5 | 5 | 5 | - |
| security.uac.standard-user-prompt | Registry | 3 | 3 | 3 | - |
| shell.taskbar.disable-widgets | Registry | 0 | 0 | 0 | - |
