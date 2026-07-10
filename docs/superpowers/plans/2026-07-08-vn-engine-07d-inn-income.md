# 07-D 여관 인과율 엔진 (수급 코어) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 06+A1+A2+B 위에 여관 상태(직원/내구도/메뉴)와 웨이브당 손님→인과율/골드 수급 산식을 순수 코어로 얹어, 07-C 인과율 소비가 의존할 "여관이 회차 진행 중 얼마나 생산하나"를 실체화한다.

**Architecture:** 전부 순수 C#(`Assets/Scripts/VNEngine/Core/Sim/**`, UnityEngine·System.IO 미참조). 여관 상태 `InnState`는 회차 넘어 유지되므로 **메타 귀속**(06 `MetaState`에 `Heroes`를 얹은 것과 동일 패턴으로 배선) + 세이브 평면 직렬화. 수급은 데이터 주도 순수 규칙(`InnIncomeRule.Compute`)으로, 완화(제곱근)+상한(cap)을 걸어 인플레를 막는다. 산식이 요구하는 정수제곱근 `IntMath.Isqrt`는 **이 슬라이스에서 신규 도입**(A1에 정수제곱근 유틸이 존재하지 않음 — 프롬프트 전제 정정). 내구도 게이트/자연감소는 별도 순수 규칙. 상태는 불변 — 모든 Compute/Decay/With는 순수.

**Tech Stack:** C# (Unity 2021+ EditMode/NUnit), 기존 `VNEngine` 네임스페이스, `VNEngine.Core.asmdef`(하위폴더 재귀 포함). 테스트는 UnityMCP `run_tests`(EditMode)로 실행.

## Global Constraints

- **순수 코어:** `Core/Sim/**`의 신규 타입은 `UnityEngine`·`System.IO`를 참조하지 않는다.
- **불변 상태:** 모든 규칙/상태는 입력을 변형하지 않고 새 값/결과를 반환한다. `InnState`는 방어적(불변 필드), 변경은 새 인스턴스.
- **데이터 주도:** 계수·상한·감소량은 전부 명명된 `const`로 규칙 타입 안에. 매직넘버 금지.
- **정수 결정론:** 부동소수 금지(`Math.Sqrt`/`float`/`double` 사용 금지). 제곱근은 정수 `IntMath.Isqrt`(이진탐색, 오버플로 회피). 이 슬라이스는 난수를 사용하지 않는다(수급은 결정론적 산식).
- **네임스페이스:** 프로덕션 타입은 `namespace VNEngine`, 테스트는 `namespace VNEngine.Tests`.
- **파일 배치:** 여관 상태/규칙/정수수학 = `Assets/Scripts/VNEngine/Core/Sim/Economy/`(07-B에서 생성된 폴더, asmdef 재귀 포함). 메타 배선 = `Core/Sim/MetaState.cs`, 세이브 = `Core/Sim/CampaignSaveData.cs`·`CampaignSave.cs`. 테스트 = `Assets/Tests/Editor/`.
- **세이브 버전:** `CampaignSaveVersion`은 **1 유지**(inn 필드는 additive — 기존 세이브는 누락 int가 JsonUtility 기본값 0으로 파싱되어 `Decor=0`=게이트 닫힘, 안전 기본). `stats` 필드를 additive로 넣었을 때와 동일 방침.
- **컴파일 확인:** 각 태스크 후 UnityMCP `read_console`로 컴파일 에러 0 확인 후 `run_tests`(EditMode). Unity 에디터가 켜져 있어야 함.
- **커밋:** 각 태스크 종료 시 커밋. 메시지 말미에 프로젝트 규약(Co-Authored-By / Claude-Session) 부착. 무관 워킹트리 파일(docs/engine/*, Fonts, gemini-*.md, .superpowers/* 등) 미커밋 유지. `git add <구체경로>`만 사용(`git add -A` 금지).

## Design Resolutions (프롬프트 위에서 코드 간극을 해소한 판단 — 리뷰 게이트에서 확인)

1. **`isqrt` 신규 도입(정정).** 프롬프트는 "isqrt는 A1의 정수제곱근 재사용"이라 했으나 VNEngine 전역에 정수제곱근 유틸이 **없다**. `IntMath.Isqrt`를 이 슬라이스 Task 1로 신설(이진탐색, `mid <= n/mid`로 오버플로 회피). 도메인은 작지만(guests≤25) 일반 정확한 구현.
2. **`InnState`는 sealed class + `Empty`**(HeroStats/MetaState.Heroes 패턴 그대로). MetaState 생성자가 `inn ?? InnState.Empty`로 null-안전 하려면 참조타입이 편함. 필드는 `Staff/Decor/MenuLevel`(전부 int, 생성자 가드 ≥0).
3. **여관=메타.** `InnState`를 `MetaState`에 추가하고, 기존 1-arg·2-arg 생성자를 **보존**(체이닝, 기본 `InnState.Empty`). `CampaignState`/`LoopEngine`/기존 테스트 무회귀.
4. **수급은 순수 규칙만, 턴/루프 배선은 07-C.** 07-B의 `CaptiveLedger`처럼 `InnIncomeRule.Compute`/`InnUpkeepRule.Decay`는 순수함수로 완성하되 `TurnEngine`/`LoopEngine`에 **배선하지 않는다**(자원 누적·골드 환산·소비는 07-C). MetaProjection→VN 투영도 이 슬라이스 밖.
5. **게이트 = `Decor<=0`이면 `InnIncome.Zero`.** `InnState` 가드가 Decor≥0을 보장하므로 실질 `Decor==0`. 게이트는 `Compute` 안에 인라인(별도 타입 불필요 — YAGNI). 자연감소는 별도 `InnUpkeepRule.Decay`(웨이브/정비일 단위 상수).
6. **손님수는 던전레벨 파라미터를 아예 받지 않음.** `Compute(InnState)` 시그니처에 dungeonLevel이 없다 → 던전레벨 연동 폭주(시뮬5차)가 **구조적으로 불가능**. 검증 테스트가 이 불변식을 못박음.
7. **`InnIncome`은 readonly struct**(Gold/Karma/Guests). Guests는 산출 근거(검증·디버그용) 포함.

---

### Task 1: 정수제곱근 유틸 (IntMath.Isqrt)

수급 골드 산식이 요구하는 내림 정수제곱근을 순수·결정론으로 신설. 부동소수 금지 원칙상 `Math.Sqrt` 불가.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/IntMath.cs`
- Test: `Assets/Tests/Editor/IntMathTests.cs` (신규)

**Interfaces:**
- Produces: `static class IntMath { static int Isqrt(int n); }` — `floor(sqrt(n))`, `n≥0`. `n<0`이면 `ArgumentOutOfRangeException`.
- Consumes: 없음.

- [ ] **Step 1: 실패 테스트 작성** — `IntMathTests.cs`.

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class IntMathTests
    {
        [Test]
        public void IsqrtOfPerfectSquares()
        {
            Assert.AreEqual(0, IntMath.Isqrt(0));
            Assert.AreEqual(1, IntMath.Isqrt(1));
            Assert.AreEqual(2, IntMath.Isqrt(4));
            Assert.AreEqual(5, IntMath.Isqrt(25));
        }

        [Test]
        public void IsqrtFloorsNonPerfectSquares()
        {
            Assert.AreEqual(1, IntMath.Isqrt(3));
            Assert.AreEqual(4, IntMath.Isqrt(24)); // 4^2=16<=24<25=5^2
            Assert.AreEqual(3, IntMath.Isqrt(15));
        }

        [Test]
        public void IsqrtNegativeThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => IntMath.Isqrt(-1));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(UnityMCP run_tests, EditMode, test_names `IntMathTests`): FAIL(IntMath 미정의/컴파일 에러). read_console로 확인.

- [ ] **Step 3: 구현** — `IntMath.cs`.

```csharp
namespace VNEngine
{
    // 정수 전용 수학 유틸(부동소수 금지 원칙). 현재 필요최소 — Isqrt(내림 정수제곱근)만.
    public static class IntMath
    {
        // floor(sqrt(n)). n>=0. 이진탐색 — Math.Sqrt/부동소수 미사용(결정론), mid*mid 대신 mid<=n/mid 로 오버플로 회피.
        public static int Isqrt(int n)
        {
            if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n), "n must be non-negative");
            if (n < 2) return n;
            int lo = 1, hi = n, ans = 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (mid <= n / mid) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans;
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run(IntMathTests): PASS. read_console 에러 0.

- [ ] **Step 5: 커밋** — `git add Assets/Scripts/VNEngine/Core/Sim/Economy/IntMath.cs Assets/Scripts/VNEngine/Core/Sim/Economy/IntMath.cs.meta Assets/Tests/Editor/IntMathTests.cs Assets/Tests/Editor/IntMathTests.cs.meta` 후 커밋 `feat(sim): integer sqrt util (07-D task1)`.

---

### Task 2: 여관 상태 (InnState — 불변, 메타 귀속)

직원/내구도/메뉴레벨을 담는 불변 상태. HeroStats 패턴(불변 + `Empty` + With-변경).

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/InnState.cs`
- Test: `Assets/Tests/Editor/InnStateTests.cs` (신규)

**Interfaces:**
- Produces: `sealed class InnState { int Staff; int Decor; int MenuLevel; static InnState Empty; InnState(int staff, int decor, int menuLevel); InnState WithDecor(int decor); }`. 생성자 가드: 각 필드 ≥0 아니면 `ArgumentException`.
- Consumes: 없음.

- [ ] **Step 1: 실패 테스트 작성** — `InnStateTests.cs`.

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnStateTests
    {
        [Test]
        public void ConstructsAndExposesFields()
        {
            var inn = new InnState(3, 5, 2);
            Assert.AreEqual(3, inn.Staff);
            Assert.AreEqual(5, inn.Decor);
            Assert.AreEqual(2, inn.MenuLevel);
        }

        [Test]
        public void EmptyIsAllZero()
        {
            Assert.AreEqual(0, InnState.Empty.Staff);
            Assert.AreEqual(0, InnState.Empty.Decor);
            Assert.AreEqual(0, InnState.Empty.MenuLevel);
        }

        [Test]
        public void WithDecorReturnsNewInstanceLeavingOriginalUnchanged()
        {
            var inn = new InnState(3, 5, 2);
            var repaired = inn.WithDecor(10);
            Assert.AreEqual(10, repaired.Decor);
            Assert.AreEqual(5, inn.Decor, "원본 불변");
            Assert.AreEqual(3, repaired.Staff, "다른 필드 보존");
            Assert.AreEqual(2, repaired.MenuLevel);
        }

        [Test]
        public void NegativeFieldsThrow()
        {
            Assert.Throws<System.ArgumentException>(() => new InnState(-1, 0, 0));
            Assert.Throws<System.ArgumentException>(() => new InnState(0, -1, 0));
            Assert.Throws<System.ArgumentException>(() => new InnState(0, 0, -1));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(InnStateTests): FAIL(InnState 미정의).

- [ ] **Step 3: 구현** — `InnState.cs`.

```csharp
namespace VNEngine
{
    // 여관 상태(메타 귀속 — 회차 넘어 유지). 직원/내구도(Decor)/메뉴레벨. 불변: 변경은 새 InnState 반환.
    // 골드 소모(고용/수리/개발 비용)는 07-C. 여기선 상태 보관 + 수급 규칙(InnIncomeRule)의 입력.
    public sealed class InnState
    {
        public int Staff { get; }
        public int Decor { get; }
        public int MenuLevel { get; }

        public static readonly InnState Empty = new InnState(0, 0, 0);

        public InnState(int staff, int decor, int menuLevel)
        {
            if (staff < 0) throw new System.ArgumentException("staff must be non-negative", nameof(staff));
            if (decor < 0) throw new System.ArgumentException("decor must be non-negative", nameof(decor));
            if (menuLevel < 0) throw new System.ArgumentException("menuLevel must be non-negative", nameof(menuLevel));
            Staff = staff; Decor = decor; MenuLevel = menuLevel;
        }

        // 내구도만 교체(자연감소/수리용). 나머지 필드 보존.
        public InnState WithDecor(int decor) => new InnState(Staff, decor, MenuLevel);
    }
}
```

- [ ] **Step 4: 통과 확인** — Run(InnStateTests): PASS. read_console 에러 0.

- [ ] **Step 5: 커밋** — `git add` 구체경로(InnState.cs+.meta, InnStateTests.cs+.meta) 후 `feat(sim): inn state (immutable, meta-owned) (07-D task2)`.

---

### Task 3: 메타 배선 + 세이브 직렬화 (MetaState.Inn + CampaignSave)

`InnState`를 `MetaState`에 얹고(기존 생성자 보존), 세이브데이터에 평면 필드로 직렬화. 기존 세이브 무회귀.

**Files:**
- Modify: `Assets/Scripts/VNEngine/Core/Sim/MetaState.cs` (Inn 프로퍼티 + 3-arg 생성자, 1-arg/2-arg 보존)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/CampaignSaveData.cs` (innStaff/innDecor/innMenuLevel int 필드)
- Modify: `Assets/Scripts/VNEngine/Core/Sim/CampaignSave.cs` (Capture/Restore에 inn 배선)
- Test: `Assets/Tests/Editor/InnPersistenceTests.cs` (신규)

**Interfaces:**
- Consumes: `InnState`/`InnState.Empty`(T2), `MetaState`/`HeroStats`/`CampaignState`/`RunState`(기존).
- Produces:
  - `MetaState`에 `InnState Inn { get; }`. 생성자: `MetaState(int loopCount)`, `MetaState(int loopCount, HeroStats heroes)`(둘 다 기존 시그니처 보존), `MetaState(int loopCount, HeroStats heroes, InnState inn)`(신규 전체).
  - `CampaignSaveData`에 `public int innStaff; public int innDecor; public int innMenuLevel;`(기본 0). `CampaignSaveVersion` 불변(=1).
  - `CampaignSave.Capture`가 `c.Meta.Inn`을 3필드로, `Restore`가 3필드→`new InnState(...)`→`new MetaState(loopCount, heroes, inn)`.

- [ ] **Step 1: 실패 테스트 작성** — `InnPersistenceTests.cs`.

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnPersistenceTests
    {
        [Test]
        public void MetaStateDefaultsInnToEmpty()
        {
            Assert.AreEqual(0, new MetaState(1).Inn.Decor, "1-arg 생성자 → Inn=Empty");
            Assert.AreEqual(0, new MetaState(1, HeroStats.Empty).Inn.Staff, "2-arg 생성자 → Inn=Empty");
        }

        [Test]
        public void MetaStateThreeArgCarriesInn()
        {
            var meta = new MetaState(2, HeroStats.Empty, new InnState(4, 7, 3));
            Assert.AreEqual(4, meta.Inn.Staff);
            Assert.AreEqual(7, meta.Inn.Decor);
            Assert.AreEqual(3, meta.Inn.MenuLevel);
        }

        [Test]
        public void CaptureRestoreRoundTripsInn()
        {
            var state = new CampaignState(
                new MetaState(5, HeroStats.Empty, new InnState(6, 9, 2)),
                new RunState(3, new Dictionary<string, int>()));
            var restored = CampaignSave.Restore(CampaignSave.Capture(state));
            Assert.AreEqual(6, restored.Meta.Inn.Staff);
            Assert.AreEqual(9, restored.Meta.Inn.Decor);
            Assert.AreEqual(2, restored.Meta.Inn.MenuLevel);
        }

        [Test]
        public void RestoreOfSaveDataWithDefaultInnFieldsYieldsClosedGate()
        {
            // 구세이브 모사: inn 필드 미기록 → JsonUtility 기본 0 → Decor=0(게이트 닫힘).
            var data = new CampaignSaveData
            {
                version = CampaignSaveData.CampaignSaveVersion,
                loopCount = 1,
                day = 1,
            };
            var restored = CampaignSave.Restore(data);
            Assert.AreEqual(0, restored.Meta.Inn.Decor, "inn 미기록 세이브는 Decor=0으로 복원");
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(InnPersistenceTests): FAIL(MetaState.Inn/3-arg·CampaignSaveData.inn* 미정의).

- [ ] **Step 3: MetaState 확장** — `MetaState.cs` 전체 교체.

```csharp
namespace VNEngine
{
    public sealed class MetaState
    {
        public int LoopCount { get; }
        public HeroStats Heroes { get; }
        public InnState Inn { get; }

        public MetaState(int loopCount) : this(loopCount, HeroStats.Empty, InnState.Empty) { }

        public MetaState(int loopCount, HeroStats heroes) : this(loopCount, heroes, InnState.Empty) { }

        public MetaState(int loopCount, HeroStats heroes, InnState inn)
        {
            LoopCount = loopCount;
            Heroes = heroes ?? HeroStats.Empty;
            Inn = inn ?? InnState.Empty;
        }
    }
}
```

- [ ] **Step 4: CampaignSaveData 확장** — `CampaignSaveData.cs`의 클래스에 필드 추가(버전 불변). `stats` 줄 아래에:

```csharp
        // Meta.Inn 평면화(additive: 구세이브는 누락 int→JsonUtility 기본 0→Decor=0 게이트닫힘). 버전 불변.
        public int innStaff;
        public int innDecor;
        public int innMenuLevel;
```

- [ ] **Step 5: CampaignSave 배선** — `CampaignSave.cs`. Capture의 객체 이니셜라이저에 3필드 추가:

```csharp
            var data = new CampaignSaveData
            {
                version = CampaignSaveData.CampaignSaveVersion,
                loopCount = c.Meta.LoopCount,
                day = c.Run.Day,
                innStaff = c.Meta.Inn.Staff,
                innDecor = c.Meta.Inn.Decor,
                innMenuLevel = c.Meta.Inn.MenuLevel,
            };
```

Restore의 반환부에서 inn 복원 후 MetaState 3-arg로:

```csharp
            var inn = new InnState(data.innStaff, data.innDecor, data.innMenuLevel);
            // RunState/HeroStats/InnState 생성자가 각각 방어적 복사 → 세이브데이터와 참조 분리.
            return new CampaignState(new MetaState(data.loopCount, heroes, inn), new RunState(data.day, res));
```

- [ ] **Step 6: 통과 확인 + 무회귀** — Run(all EditMode): InnPersistenceTests PASS + 기존 MetaState/CampaignSave/LoopEngine 테스트 전건 PASS(1-arg/2-arg 생성자·기존 세이브 왕복 무회귀). read_console 에러 0.

- [ ] **Step 7: 커밋** — `git add` 구체경로(MetaState.cs, CampaignSaveData.cs, CampaignSave.cs, InnPersistenceTests.cs+.meta) 후 `feat(sim): wire InnState into MetaState + campaign save (07-D task3)`.

---

### Task 4: 여관 수급 산식 + 내구도 감소 (InnIncomeRule / InnUpkeepRule)

웨이브당 손님→인과율/골드 순수 산식(완화+상한+게이트) + 내구도 자연감소. 인플레 방지 불변식(손님수 던전레벨 무관, 골드 상한, 내구도 게이트)을 테스트로 못박음.

**Files:**
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/InnIncome.cs` (`InnIncome` readonly struct)
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/InnIncomeRule.cs`
- Create: `Assets/Scripts/VNEngine/Core/Sim/Economy/InnUpkeepRule.cs`
- Test: `Assets/Tests/Editor/InnIncomeRuleTests.cs` (신규)

**Interfaces:**
- Consumes: `InnState`(T2), `IntMath.Isqrt`(T1).
- Produces:
  - `readonly struct InnIncome { int Gold; int Karma; int Guests; static InnIncome Zero; InnIncome(int gold, int karma, int guests); }`.
  - `static class InnIncomeRule` — 상수 `GuestsPerStaff=2, MaxGuests=25, GoldPerSqrtGuest=8, GoldPerMenuLevel=3, MaxGold=300`. `InnIncome Compute(InnState inn)` — `inn.Decor<=0`이면 `InnIncome.Zero`; 아니면 `guests=min(Staff*GuestsPerStaff+MenuLevel, MaxGuests)`, `gold=min(Isqrt(guests)*GoldPerSqrtGuest+MenuLevel*GoldPerMenuLevel, MaxGold)`, `karma=guests`. **dungeonLevel 파라미터 없음**(구조적 폭주 차단).
  - `static class InnUpkeepRule` — 상수 `DecorDecayPerTick=1`. `InnState Decay(InnState inn)` = `inn.WithDecor(max(0, inn.Decor-DecorDecayPerTick))`.

- [ ] **Step 1: 실패 테스트 작성** — `InnIncomeRuleTests.cs`.

```csharp
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnIncomeRuleTests
    {
        [Test]
        public void ComputesGuestsGoldKarmaFromInnScale()
        {
            // Staff3,Menu2,Decor5: guests=min(3*2+2,25)=8; isqrt(8)=2; gold=min(2*8+2*3,300)=22; karma=8.
            var inc = InnIncomeRule.Compute(new InnState(3, 5, 2));
            Assert.AreEqual(8, inc.Guests);
            Assert.AreEqual(22, inc.Gold);
            Assert.AreEqual(8, inc.Karma);
        }

        [Test]
        public void KarmaEqualsGuests()
        {
            var inc = InnIncomeRule.Compute(new InnState(5, 5, 1)); // guests=min(11,25)=11
            Assert.AreEqual(inc.Guests, inc.Karma, "인과율이 주 산출 = 손님수 그대로");
            Assert.AreEqual(11, inc.Karma);
        }

        [Test]
        public void GuestsAreCappedAt25()
        {
            var inc = InnIncomeRule.Compute(new InnState(20, 5, 10)); // 40+10 -> cap 25
            Assert.AreEqual(25, inc.Guests);
        }

        [Test]
        public void GoldIsCappedAt300()
        {
            // Staff0,Menu100,Decor5: guests=min(100,25)=25; isqrt(25)=5; gold=min(5*8+100*3,300)=min(340,300)=300.
            var inc = InnIncomeRule.Compute(new InnState(0, 5, 100));
            Assert.AreEqual(25, inc.Guests);
            Assert.AreEqual(300, inc.Gold, "여관 골드 상한 — 던전/메뉴 올려도 폭증 안 함");
        }

        [Test]
        public void GuestsDoNotDependOnAnythingButInnScale()
        {
            // Compute는 dungeonLevel 파라미터가 아예 없음(시그니처 레벨 불변식). 같은 여관 → 항상 같은 손님수.
            var inn = new InnState(4, 5, 4); // guests=min(8+4,25)=12
            Assert.AreEqual(12, InnIncomeRule.Compute(inn).Guests);
            Assert.AreEqual(12, InnIncomeRule.Compute(inn).Guests);
        }

        [Test]
        public void ZeroDecorGatesIncomeToZero()
        {
            var inc = InnIncomeRule.Compute(new InnState(10, 0, 5)); // Decor=0 → 게이트 닫힘
            Assert.AreEqual(0, inc.Guests);
            Assert.AreEqual(0, inc.Gold);
            Assert.AreEqual(0, inc.Karma);
        }

        [Test]
        public void NullInnThrows()
        {
            Assert.Throws<System.ArgumentNullException>(() => InnIncomeRule.Compute(null));
        }

        [Test]
        public void DecayReducesDecorByOneFloorAtZero()
        {
            Assert.AreEqual(4, InnUpkeepRule.Decay(new InnState(3, 5, 2)).Decor);
            Assert.AreEqual(0, InnUpkeepRule.Decay(new InnState(3, 0, 2)).Decor, "0 미만 방지");
        }

        [Test]
        public void DecayPreservesOtherFieldsAndDoesNotMutateInput()
        {
            var inn = new InnState(3, 5, 2);
            var decayed = InnUpkeepRule.Decay(inn);
            Assert.AreEqual(3, decayed.Staff);
            Assert.AreEqual(2, decayed.MenuLevel);
            Assert.AreEqual(5, inn.Decor, "원본 불변");
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — Run(InnIncomeRuleTests): FAIL(InnIncome/InnIncomeRule/InnUpkeepRule 미정의).

- [ ] **Step 3: InnIncome 구현** — `InnIncome.cs`.

```csharp
namespace VNEngine
{
    // 여관 웨이브당 산출(불변 결과). Gold=부차, Karma=주 산출(인과율 엔진), Guests=산출 근거(검증/디버그).
    public readonly struct InnIncome
    {
        public int Gold { get; }
        public int Karma { get; }
        public int Guests { get; }

        public InnIncome(int gold, int karma, int guests) { Gold = gold; Karma = karma; Guests = guests; }

        public static readonly InnIncome Zero = new InnIncome(0, 0, 0);
    }
}
```

- [ ] **Step 4: InnIncomeRule 구현** — `InnIncomeRule.cs`.

```csharp
namespace VNEngine
{
    // 여관 수급 산식(순수·결정론·데이터주도). 완화(제곱근)+상한(cap)으로 인플레 방지(시뮬6차).
    // ★ 손님수는 여관규모(Staff/MenuLevel)에만 연동 — dungeonLevel 파라미터 없음(시뮬5차 폭주 교훈).
    // ★ 새 수입원엔 기존과 같은 완화/상한 → 여관 골드비중 목표 <10%. innKarma=guests(성장 주축).
    // 계수는 전부 초기 추정 튜닝값(플레이테스트 실측 조정 대상).
    public static class InnIncomeRule
    {
        public const int GuestsPerStaff = 2;
        public const int MaxGuests = 25;
        public const int GoldPerSqrtGuest = 8;
        public const int GoldPerMenuLevel = 3;
        public const int MaxGold = 300;

        public static InnIncome Compute(InnState inn)
        {
            if (inn == null) throw new System.ArgumentNullException(nameof(inn));
            if (inn.Decor <= 0) return InnIncome.Zero;  // 내구도 게이트: 손님 안 받음(수입 0)

            int guests = System.Math.Min(inn.Staff * GuestsPerStaff + inn.MenuLevel, MaxGuests);
            int gold = System.Math.Min(IntMath.Isqrt(guests) * GoldPerSqrtGuest + inn.MenuLevel * GoldPerMenuLevel, MaxGold);
            int karma = guests;
            return new InnIncome(gold, karma, guests);
        }
    }
}
```

- [ ] **Step 5: InnUpkeepRule 구현** — `InnUpkeepRule.cs`.

```csharp
namespace VNEngine
{
    // 여관 내구도 자연감소(웨이브/정비일 단위, 데이터 상수). 0 미만 방지. 수리(골드 소모)는 07-C.
    // Compute(수급)와 순서: C가 배선 시 게이트 판정(Decor>0) 후 Decay 적용이 의도(D는 각 규칙 순수, 배선 안 함).
    public static class InnUpkeepRule
    {
        public const int DecorDecayPerTick = 1;

        public static InnState Decay(InnState inn)
        {
            if (inn == null) throw new System.ArgumentNullException(nameof(inn));
            return inn.WithDecor(System.Math.Max(0, inn.Decor - DecorDecayPerTick));
        }
    }
}
```

- [ ] **Step 6: 통과 확인** — Run(all EditMode): InnIncomeRuleTests PASS + 무회귀. read_console 에러 0.

- [ ] **Step 7: 커밋** — `git add` 구체경로(InnIncome.cs, InnIncomeRule.cs, InnUpkeepRule.cs, InnIncomeRuleTests.cs + 각 .meta) 후 `feat(sim): inn income + upkeep rules (07-D task4)`.

---

### Task 5: 문서 갱신 (07 §7 여관)

구현 상태를 문서에 반영(A2/B 방식과 동일하게 "구현/미구현" 명확히).

**Files:**
- Modify: `docs/engine/07-defense-combat.md` (§7 여관 관련 절 — 없으면 여관 절을 찾아 갱신, 구현상태 블록 추가)
- Modify: `docs/engine/06-loop-and-state.md` (상태표 — MetaState.Inn 실구현 반영)

- [ ] **Step 1: 문서 구조 확인** — 두 문서를 읽어 여관/메타상태 관련 절의 실제 번호·제목 확인(§7이 정확히 여관인지, 아니면 실제 절 번호에 맞춰 적응). 07-B 갱신이 붙인 날짜 콜아웃 스타일을 그대로 따름.

- [ ] **Step 2: 07 여관 절 갱신** — "구현(07-D, 2026-07-08):" 블록 추가. 내용: 여관=메타 귀속(`InnState` in `MetaState`), 수급 산식(`InnIncomeRule.Compute` — guests=min(Staff*2+MenuLevel,25), gold=min(isqrt(guests)*8+MenuLevel*3,300), karma=guests), 손님수 던전레벨 무관(폭주 방지), 여관=인과율 엔진(karma 주산출·gold 부차), 내구도 게이트(Decor>0)+자연감소(`InnUpkeepRule.Decay`, DecorDecayPerTick=1), `IntMath.Isqrt` 정수제곱근. 계수는 초기 추정 튜닝값 명시.

- [ ] **Step 3: 06 상태표 갱신** — `MetaState.Inn`(InnState: Staff/Decor/MenuLevel)이 실제 구현됨·세이브 평면직렬화(innStaff/innDecor/innMenuLevel, 버전 불변 additive) 반영.

- [ ] **Step 4: 미구현/후속 스코프 명시** — (a) 수급의 턴/루프 배선·자원 누적·골드 환산·인과율 소비(스탯강화/레벨업) = 07-C; (b) 골드 소모(직원 고용비·내구도 수리비·메뉴 개발비) = 07-C; (c) 소문수집·마을파견·모험가예보 = 07-D2/이후; (d) MetaProjection→VN 투영에 Inn 미배선; (e) 계수 전부 초기 추정(실측 튜닝 대상).

- [ ] **Step 5: 커밋** — `git add docs/engine/07-defense-combat.md docs/engine/06-loop-and-state.md` 후 `docs(sim): reflect 07-D inn income implementation status`.

---

## 최종 통합 & 리뷰

- [ ] **전체 EditMode 그린** — Run(all): 신규 테스트 포함 전건 PASS. read_console 컴파일 에러/경고 0.
- [ ] **sonnet 태스크별 리뷰** — subagent-driven 규약대로 각 태스크 종료 시 리뷰 게이트(spec+quality).
- [ ] **opus 전건 whole-branch 리뷰** — 머지 전 end-to-end 검토(순수성·불변·정수결정론·데이터주도 불변식, 특히 손님수 던전레벨 무관/골드 상한/게이트, 세이브 additive 무회귀, MetaState 생성자 역호환).
- [ ] **main --no-ff 머지** — 전건 통과 후. 무관 워킹트리 파일 미커밋 유지.

## Self-Review (플랜 작성자 체크 결과)

- **스펙 커버리지:** 프롬프트 작업분해 1(InnState+MetaState배선+세이브)→T2+T3, 2(InnIncomeRule)→T4, 3(게이트+자연감소)→T4, 4(문서)→T5. 추가 T1(isqrt 신설)은 프롬프트 전제 정정. 검증항목 7개 전부 대응 테스트: 손님수 던전레벨무관→T4 GuestsDoNotDependOnAnythingButInnScale(+시그니처에 dungeonLevel 부재), gold상한300→T4 GoldIsCappedAt300, Decor=0게이트→T4 ZeroDecorGatesIncomeToZero, guests상한25→T4 GuestsAreCappedAt25, karma=guests→T4 KarmaEqualsGuests, 세이브왕복→T3 CaptureRestoreRoundTripsInn, 불변/순수→T2·T4 각 불변 테스트.
- **플레이스홀더 스캔:** 모든 코드 스텝에 실제 코드/시그니처/커밋 add 경로 명시. TBD·"적절히" 없음. 계수는 명명 const로 코드에 실재(문서에 튜닝대상 표기). 후속 스코프는 T5 Step4에 명시적 defer.
- **타입 일관성:** `IntMath.Isqrt`(T1)→T4 사용, `InnState`(T2 Staff/Decor/MenuLevel/Empty/WithDecor)→T3·T4 사용, `MetaState` 3-arg(loopCount,heroes,inn)→T3 CampaignSave 사용, `InnIncome`(Gold/Karma/Guests/Zero)→T4, 상수명 일치. `CampaignSaveVersion` 불변(additive) 일관.
- **리스크:** T3의 CampaignSaveData additive 필드가 기존 세이브 왕복을 깨지 않는지(버전 불변·JsonUtility 기본 0) — T3 RestoreOfSaveDataWithDefaultInnFields로 못박음. MetaState 1-arg/2-arg 역호환 — T3 MetaStateDefaultsInnToEmpty + 기존 전건 무회귀로 확인.
