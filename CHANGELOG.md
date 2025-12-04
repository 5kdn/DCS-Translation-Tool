# Changelog

## [1.3.1](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.3.0...v1.3.1) (2025-12-04)


### Bug Fixes

* CreatePullRequest ダイアログの変更するファイルリストが表示されないバグを修正した ([#20](https://github.com/5kdn/DCS-Translation-Tool/issues/20)) ([0b43963](https://github.com/5kdn/DCS-Translation-Tool/commit/0b43963066ff3faa33897152a26bb6d411a49e36))

## [1.3.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.2.0...v1.3.0) (2025-12-03)


### Features

* クライアントサイドでファイルをダウンロードするように変更した ([#16](https://github.com/5kdn/DCS-Translation-Tool/issues/16)) ([085437f](https://github.com/5kdn/DCS-Translation-Tool/commit/085437f64a0411cd1dadb6750acb1f39b1d29f8d))


### Bug Fixes

* ダウンロードURL経由の適用に更新し処理後の状態更新を安定化 ([#19](https://github.com/5kdn/DCS-Translation-Tool/issues/19)) ([ff5772b](https://github.com/5kdn/DCS-Translation-Tool/commit/ff5772bdacb5dc2d53e4b49a02ffdac65df5d96f))
* ファイル監視の多重通知を抑制しダウンロード中の更新漏れを防止 ([#18](https://github.com/5kdn/DCS-Translation-Tool/issues/18)) ([2aaf015](https://github.com/5kdn/DCS-Translation-Tool/commit/2aaf0153e1b2e037f6320fb59eac6a5dbb69d0a1))

## [1.2.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.1.0...v1.2.0) (2025-12-01)


### Features

* treat .trk file as zip ([#14](https://github.com/5kdn/DCS-Translation-Tool/issues/14)) ([3ce1d91](https://github.com/5kdn/DCS-Translation-Tool/commit/3ce1d9197081847cbc14e64e8594dfc225b199e5)), closes [#8](https://github.com/5kdn/DCS-Translation-Tool/issues/8)


### Bug Fixes

* change branch prefix to feature/ ([#12](https://github.com/5kdn/DCS-Translation-Tool/issues/12)) ([94e67e4](https://github.com/5kdn/DCS-Translation-Tool/commit/94e67e4deb7eeeee6310158386a72d05e20023d2)), closes [#9](https://github.com/5kdn/DCS-Translation-Tool/issues/9)

## [1.1.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.0.0...v1.1.0) (2025-11-19)


### Features

* Apply時 *.miz ディレクトリ配下ではないファイルを直接Source*Dirに保存する機能を追加 ([#2](https://github.com/5kdn/DCS-Translation-Tool/issues/2)) ([6327677](https://github.com/5kdn/DCS-Translation-Tool/commit/632767784d1a62dc12548b2c1308fcedca915500))

## [1.0.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v0.0.1...v1.0.0) (2025-11-17)


### Features

* add ApiService ([b4b1ae8](https://github.com/5kdn/DCS-Translation-Tool/commit/b4b1ae83c1f50e4f8e99e547672136892b322b78))
* add AppSettingsService ([89f76e3](https://github.com/5kdn/DCS-Translation-Tool/commit/89f76e33715546e9192c95ba740c70e4ab5cc0f1))
* add EnvironmentProvider ([632a3ac](https://github.com/5kdn/DCS-Translation-Tool/commit/632a3ace2c813579ed82b24640bf8926946c9579))
* add FileEntryService ([8990e0b](https://github.com/5kdn/DCS-Translation-Tool/commit/8990e0b28f5d3ca776bdfc7492c3b30f9569371d))
* add FileService ([f211415](https://github.com/5kdn/DCS-Translation-Tool/commit/f211415bee7bc83f0dd80cc15d0369b05bebe293))
* add LoggingService ([894a090](https://github.com/5kdn/DCS-Translation-Tool/commit/894a09072acb0fc0a0dd66b02567978605152b47))
* add SnackbarService ([4795fbd](https://github.com/5kdn/DCS-Translation-Tool/commit/4795fbd613b875c4b3e05ce5a9e836301b8f5117))
* add SystemService ([02e05a7](https://github.com/5kdn/DCS-Translation-Tool/commit/02e05a774fb7bf2e9f8b2235e858f85b4c6679de))
* add ZipService ([a3bd69a](https://github.com/5kdn/DCS-Translation-Tool/commit/a3bd69a16c212efff6ff7145832f479c08929a9d))
* implement CreatePullRequestView and ([56a6e72](https://github.com/5kdn/DCS-Translation-Tool/commit/56a6e72948d1cd43aba936bf8573ec9c20601815))
* implement DownloadView and DownloadViewModel ([f23fc10](https://github.com/5kdn/DCS-Translation-Tool/commit/f23fc101e4290b106a8a3dfa5f4b2e965b01c306))
* implement MainView and MainViewModel ([b2f7a2c](https://github.com/5kdn/DCS-Translation-Tool/commit/b2f7a2cbf1d1a74e094d7beb69e2060cb21b451b))
* implement SettingsView and SettingsViewModel ([0b4b1b7](https://github.com/5kdn/DCS-Translation-Tool/commit/0b4b1b72330b7234df8a7de2f1267f89884945a9))
* implement ShellView and ShellViewModel ([b9651fc](https://github.com/5kdn/DCS-Translation-Tool/commit/b9651fcd6767945d43508faae0777937baa85f13))
* implement UploadView and UploadViewModel ([ae71aa8](https://github.com/5kdn/DCS-Translation-Tool/commit/ae71aa805672c0ea2c92e2e74c3d699bf5dd4c4e))


### Miscellaneous Chores

* release 1.0.0 ([41b2bdc](https://github.com/5kdn/DCS-Translation-Tool/commit/41b2bdcd138ae875a574a180c5924c7134b5ddc9))
