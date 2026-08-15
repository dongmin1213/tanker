using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanker
{
    public enum SkillType { None, Taunt, Cover, Brace }
    public enum Phase { Player, Resolving, Won, Lost }

    /// 전투 규칙의 전부. UI는 이 상태를 읽기만 한다.
    public class BattleManager : MonoBehaviour
    {
        public Unit Tank, Dps, Healer;
        public Unit GoblinA, GoblinB, Brute;
        public readonly List<Unit> Allies = new();
        public readonly List<Unit> Enemies = new();

        public Phase Phase = Phase.Player;
        public int Turn = 1;
        public int TauntCooldown;                 // 0이면 사용 가능
        public const int TauntCooldownMax = 3;
        public bool SkillUsed;                    // 턴당 1스킬
        public SkillType Pending = SkillType.None; // 대상 선택 대기 중 스킬
        public Unit CoverTarget;                  // 이번 턴 엄호 대상
        public bool Bracing;                      // 이번 턴 버티기
        public int TotalSaved;                    // 누적 '막아낸' 데미지 (점수판)
        public string Log = "";

        public event Action<Unit, string, Color> Popup; // 유닛 위 플로팅 텍스트
        public event Action<Unit, Unit> Strike;         // (공격자, 피격자) — 연출용

        void Awake()
        {
            Setup();
        }

        void Setup()
        {
            Allies.Clear(); Enemies.Clear();
            Tank = Unit.Make("나", Team.Ally, 40, 0, tank: true);
            Dps = Unit.Make("딜러", Team.Ally, 18, 6);
            Healer = Unit.Make("힐러", Team.Ally, 14, 5);
            Allies.AddRange(new[] { Tank, Dps, Healer });

            GoblinA = Unit.Make("고블린A", Team.Enemy, 12, 4);
            GoblinB = Unit.Make("고블린B", Team.Enemy, 12, 4);
            Brute = Unit.Make("브루트", Team.Enemy, 22, 8);
            Enemies.AddRange(new[] { GoblinA, GoblinB, Brute });

            Phase = Phase.Player; Turn = 1; TauntCooldown = 0; SkillUsed = false;
            Pending = SkillType.None; CoverTarget = null; Bracing = false; TotalSaved = 0;
            RollIntents();
            Log = "1턴: 적의 공격 예고를 보고 팀을 지켜라";
        }

        // ---- 인텐트 ----

        void RollIntents()
        {
            foreach (var e in Enemies)
            {
                e.Charging = false;
                if (!e.Alive) { e.Intent = null; continue; }
                if (e == Brute)
                {
                    if (!e.Enraged && e.Hp <= 10)
                    {
                        e.Enraged = true; e.Power = 6;
                        Popup?.Invoke(e, "격노!!", new Color(1f, 0.3f, 0.25f));
                        Log = "브루트가 격노했다 — 이제 매 턴 공격한다!";
                    }
                    if (e.Enraged) e.Intent = PreferredSmashTarget();
                    else if (Turn % 2 == 1) { e.Intent = null; e.Charging = true; }
                    else e.Intent = PreferredSmashTarget();
                    continue;
                }
                e.Intent = e == GoblinA ? LowestHpBackliner()
                         : (Dps.Alive ? Dps : LowestHpBackliner());
            }
        }

        Unit LowestHpBackliner()
        {
            Unit best = null;
            foreach (var u in new[] { Healer, Dps })
                if (u.Alive && (best == null || u.Hp < best.Hp)) best = u;
            return best ?? Tank;
        }

        Unit PreferredSmashTarget() => Dps.Alive ? Dps : (Healer.Alive ? Healer : Tank);

        /// 도발/엄호를 반영한 실제 공격 대상. UI 화살표도 이걸 그린다.
        public Unit EffectiveTarget(Unit enemy)
        {
            if (enemy.Intent == null) return null;
            if (enemy.TauntTurns > 0) return Tank;
            if (CoverTarget != null && enemy.Intent == CoverTarget) return Tank;
            return enemy.Intent;
        }

        // ---- 플레이어 입력 (UI가 호출) ----

        public bool CanUseSkill => Phase == Phase.Player && !SkillUsed;
        public bool TauntReady => CanUseSkill && TauntCooldown == 0;

        public void PressSkill(SkillType s)
        {
            if (!CanUseSkill) return;
            if (s == SkillType.Taunt && TauntCooldown > 0) return;
            if (s == SkillType.Brace)
            {
                Bracing = true; SkillUsed = true; Pending = SkillType.None;
                Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + 3);
                Popup?.Invoke(Tank, "+3 버티기", new Color(1f, 0.84f, 0.37f));
                Log = "버티기: 이번 턴 받는 피해 절반 (+3 회복)";
                return;
            }
            Pending = Pending == s ? SkillType.None : s; // 같은 버튼 다시 누르면 취소
            Log = Pending == SkillType.None ? "스킬 선택 취소"
                : Pending == SkillType.Taunt ? "도발할 적을 선택" : "엄호할 아군을 선택";
        }

        public void ClickUnit(Unit u)
        {
            if (Phase != Phase.Player || Pending == SkillType.None || !u.Alive) return;
            if (Pending == SkillType.Taunt && u.Team == Team.Enemy)
            {
                u.TauntTurns = 2; TauntCooldown = TauntCooldownMax; SkillUsed = true;
                Pending = SkillType.None;
                Popup?.Invoke(u, "도발!", new Color(1f, 0.55f, 0.35f));
                Log = u.Name + " 도발 — 2턴간 나만 공격한다";
            }
            else if (Pending == SkillType.Cover && u.Team == Team.Ally && !u.IsTank)
            {
                CoverTarget = u; SkillUsed = true; Pending = SkillType.None;
                Popup?.Invoke(u, "엄호", new Color(0.55f, 0.75f, 1f));
                Log = u.Name + " 엄호 — 이번 턴 그를 노리는 공격은 내가 맞는다";
            }
        }

        public void EndTurn()
        {
            if (Phase != Phase.Player) return;
            Pending = SkillType.None;
            StartCoroutine(Resolve());
        }

        public void Restart()
        {
            StopAllCoroutines();
            Setup();
        }

        // ---- 턴 해소 ----

        IEnumerator Resolve()
        {
            Phase = Phase.Resolving;
            var wait = new WaitForSeconds(0.55f);

            // 딜러 행동 (힐러는 적 페이즈 뒤에 행동한다 — 상처가 난 뒤 치료)
            if (Dps.Alive)
            {
                var target = LowestHpEnemy();
                if (target != null)
                {
                    int dmg = Dps.Shaken ? Dps.Power / 2 : Dps.Power;
                    Dps.Shaken = false;
                    Strike?.Invoke(Dps, target);
                    target.Hp = Mathf.Max(0, target.Hp - dmg);
                    Popup?.Invoke(target, "-" + dmg, Color.white);
                    Log = "딜러가 " + target.Name + "에게 " + dmg + " 피해";
                    yield return wait;
                }
            }

            // 적 행동
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                if (e.Charging) { Log = e.Name + "이(가) 힘을 모은다..."; yield return wait; continue; }
                var planned = e.Intent;
                if (planned == null || !planned.Alive) planned = Tank;
                var actual = EffectiveTarget(e) ?? Tank;
                if (!actual.Alive) actual = Tank;

                int dmg = e.Power;
                int saved = 0;
                if (actual.IsTank && Bracing) { saved += dmg - dmg / 2; dmg /= 2; }
                Strike?.Invoke(e, actual);
                actual.Hp = Mathf.Max(0, actual.Hp - dmg);

                if (actual.IsTank && planned != null && !planned.IsTank)
                {
                    TotalSaved += e.Power;
                    Popup?.Invoke(Tank, "-" + dmg + " 대신 맞음!", new Color(1f, 0.84f, 0.37f));
                    Log = planned.Name + "을(를) 노린 공격을 내가 받아냈다 (" + dmg + ")";
                }
                else
                {
                    if (saved > 0) { TotalSaved += saved; }
                    Popup?.Invoke(actual, "-" + dmg, actual.IsTank ? new Color(1f, 0.84f, 0.37f) : new Color(1f, 0.45f, 0.45f));
                    Log = e.Name + "이(가) " + actual.Name + "에게 " + dmg + " 피해";
                }
                if (!actual.IsTank) { actual.Shaken = true; Popup?.Invoke(actual, "위축!", new Color(1f, 0.6f, 0.9f)); }
                if (e.TauntTurns > 0) e.TauntTurns--;
                yield return wait;
            }

            // 힐러 행동 — 적 페이즈가 끝난 뒤 가장 아픈 아군을 치료
            if (Healer.Alive)
            {
                var target = LowestHpAlly();
                if (target != null)
                {
                    int amount = Healer.Shaken ? Healer.Power / 2 : Healer.Power;
                    Healer.Shaken = false;
                    Strike?.Invoke(Healer, target);
                    target.Hp = Mathf.Min(target.MaxHp, target.Hp + amount);
                    Popup?.Invoke(target, "+" + amount, new Color(0.55f, 1f, 0.55f));
                    Log = "힐러가 " + target.Name + "을(를) " + amount + " 회복";
                    yield return wait;
                }
            }

            // 턴 정리
            if (!AnyEnemyAlive()) { Phase = Phase.Won; Log = "던전 클리어! 막아낸 피해 " + TotalSaved; yield break; }
            foreach (var a in Allies)
                if (!a.Alive) { Phase = Phase.Lost; Log = a.Name + " 사망... 탱커의 실패다"; yield break; }

            Turn++;
            SkillUsed = false; Bracing = false; CoverTarget = null;
            if (TauntCooldown > 0) TauntCooldown--;
            RollIntents();
            Phase = Phase.Player;
            Log = Turn + "턴: 예고를 읽고 결정하라";
        }

        Unit LowestHpEnemy()
        {
            Unit best = null;
            foreach (var e in Enemies) if (e.Alive && (best == null || e.Hp < best.Hp)) best = e;
            return best;
        }

        Unit LowestHpAlly()
        {
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && a.Hp < a.MaxHp && (best == null || a.Hp < best.Hp)) best = a;
            return best;
        }

        bool AnyEnemyAlive()
        {
            foreach (var e in Enemies) if (e.Alive) return true;
            return false;
        }
    }
}
