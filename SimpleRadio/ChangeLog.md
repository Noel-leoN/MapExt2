## v1.0.2 - ExtendedRadio Compatibility Fix

* **[Compatibility]:** Fixed custom stations potentially being lost when coexisting with ExtendedRadio, due to non-deterministic Harmony Postfix execution order on `Radio.LoadRadio`.

---

### 主要改动

* **[兼容性]：** 修复与 ExtendedRadio 共存时，因 Harmony Postfix 执行顺序不确定导致自定义电台可能被覆盖丢失的问题。
