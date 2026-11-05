"""Read-only APK manifest probes for CatLife interview question 10. No device changes."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import unittest


class ReleasePackageAudit(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        def dump(*args):
            return subprocess.check_output([OPTIONS.aapt, "dump", *args], encoding="utf-8", errors="replace")
        cls.badging = dump("badging", OPTIONS.apk)
        cls.manifest = dump("xmltree", OPTIONS.apk, "AndroidManifest.xml")
        cls.permissions = re.findall(r"uses-permission: name='([^']+)'", cls.badging)

    def test_01_no_all_files_access_for_offline_focus_and_cloud_feedback(self):
        self.assertNotIn("android.permission.MANAGE_EXTERNAL_STORAGE", self.permissions)

    def test_02_release_candidate_is_not_debuggable(self):
        self.assertNotIn("application-debuggable", self.badging)
        self.assertIsNone(re.search(r"android:debuggable[^\n]*0xffffffff", self.manifest))

    def test_03_no_camera_mic_contacts_or_sms_permissions(self):
        unexpected = set(self.permissions) & {
            "android.permission.CAMERA", "android.permission.RECORD_AUDIO",
            "android.permission.READ_CONTACTS", "android.permission.READ_SMS"}
        self.assertEqual(unexpected, set())

    def test_04_review_package_and_version_are_explicit(self):
        self.assertIn("name='com.catlife.review.ui' versionCode='3' versionName='0.3.0'", self.badging)

    def test_05_internet_is_declared_without_explicit_cleartext_opt_in(self):
        self.assertIn("android.permission.INTERNET", self.permissions)
        self.assertIsNone(re.search(r"android:usesCleartextTraffic[^\n]*0xffffffff", self.manifest))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--apk", required=True)
    parser.add_argument("--aapt", required=True)
    parser.add_argument("--output", required=True)
    OPTIONS = parser.parse_args()
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ReleasePackageAudit))
    output = Path(OPTIONS.output)
    output.mkdir(parents=True, exist_ok=True)
    summary = {
        "apk": OPTIONS.apk, "tests": result.testsRun,
        "failures": [case.id().split(".")[-1] for case, _ in result.failures],
        "errors": [case.id().split(".")[-1] for case, _ in result.errors],
        "permissions": ReleasePackageAudit.permissions,
        "identity": ReleasePackageAudit.badging.splitlines()[0],
        "backupAttributesExplicit": [name for name in ("allowBackup", "fullBackupContent", "dataExtractionRules")
                                      if "android:" + name in ReleasePackageAudit.manifest],
        "scope": "Static manifest only; no permission grant, file access or backup operation was executed."
    }
    (output / "package-audit.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    (output / "AndroidManifest.txt").write_text(ReleasePackageAudit.manifest, encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
