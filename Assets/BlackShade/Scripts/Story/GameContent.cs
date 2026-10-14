using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // ==================================================================== 아이템
    public enum ItemCategory { Important, Key, Recovery, Consumable }
    public enum ItemUse { None, Heal, Drink, EquipCan, Flashlight, Battery, ViewScore, OnlyInPlace }

    public class ItemDef
    {
        public string id, name, desc;
        public ItemCategory category;
        public ItemUse use;
        public float amount;          // 회복량 / 배터리량
        public string placeHint;      // OnlyInPlace일 때 안내 문구
        public Color color = new Color(0.8f, 0.8f, 0.8f);
    }

    public static class ItemDB
    {
        public static readonly string[] CategoryNames = { "중요", "열쇠", "회복", "소비" };
        static Dictionary<string, ItemDef> db;

        public static ItemDef Get(string id)
        {
            if (db == null) Build();
            ItemDef d;
            return db.TryGetValue(id, out d) ? d : null;
        }

        public static string Name(string id) { ItemDef d = Get(id); return d != null ? d.name : id; }

        static void Add(string id, string name, ItemCategory cat, ItemUse use, float amount, string desc, Color col, string hint = null)
        {
            db[id] = new ItemDef { id = id, name = name, category = cat, use = use, amount = amount, desc = desc, color = col, placeHint = hint };
        }

        static void Build()
        {
            db = new Dictionary<string, ItemDef>();
            Color key = new Color(0.86f, 0.74f, 0.42f);
            Color red = new Color(0.86f, 0.36f, 0.36f);
            Color blue = new Color(0.5f, 0.66f, 0.9f);
            Color paper = new Color(0.92f, 0.9f, 0.84f);
            Color pink = new Color(0.95f, 0.55f, 0.68f);

            // ---- 중요
            Add("flashlight", "비상 손전등", ItemCategory.Important, ItemUse.Flashlight, 0,
                "각 층에 비치된 비상용 손전등. [F] 키로 켜고 끈다.\n배터리가 다 닳으면 꺼진다. 다른 층의 비상용품함이나 건전지로 채울 수 있다.", blue);
            Add("score_piece", "찢어진 악보 조각", ItemCategory.Important, ItemUse.ViewScore, 0,
                "누렇게 바랜 오선지 조각. 졸업가의 일부가 적혀 있다.\n모두 모으면 노래를 끝까지 부를 수 있을 것 같다.", paper);
            Add("score_full", "졸업가 악보", ItemCategory.Important, ItemUse.ViewScore, 0,
                "찢어진 조각들을 이어 붙인 졸업가 악보.\n이제 노래를 처음부터 끝까지 부를 수 있다.", paper);
            Add("badge", "철쭉 명찰 장식", ItemCategory.Important, ItemUse.OnlyInPlace, 0,
                "교화인 철쭉 모양으로 만든 금속 명찰 장식. 앞면에 '축 졸업 하'라고 새겨져 있고, 그 뒤의 글자는 긁혀서 지워져 있다.\n졸업생에게 달아 주던 장식이다.", pink,
                "졸업생만 들어갈 수 있는 곳에서 쓸 수 있을 것 같다.");
            Add("fountain_pen", "압수물품: 2학년 ■■■의 만년필", ItemCategory.Important, ItemUse.OnlyInPlace, 0,
                "2학년 교무실 책상 위, 압수물품 쪽지와 함께 놓여 있던 낡은 만년필. 주인의 이름은 검게 칠해져 읽을 수 없다.\n손에 쥐면 이상하게 따뜻하다.", new Color(0.3f, 0.3f, 0.35f),
                "지금은 쓸 곳이 없다. 언젠가 쓸모가 있을지도 모른다.");

            // ---- 열쇠
            string keyHint = "잠긴 문 앞에서 상호작용하면 사용할 수 있다.";
            Add("key_admin", "행정실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "'행정실'이라고 적힌 이름표가 달린 열쇠. 소화전함 안에 숨겨져 있었다.", key, keyHint);
            Add("key_science", "과학실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "1학년 교무실 열쇠함에 걸려 있던 과학실 열쇠.", key, keyHint);
            Add("key_staff2", "2학년 교무실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "2학년 2반 교탁 서랍에 들어 있던 열쇠. 주번이 깜빡하고 둔 것 같다.", key, keyHint);
            Add("key_nurse", "양호실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "2학년 교무실 열쇠함에 걸려 있던 양호실 열쇠. 이름표에 붉은 얼룩이 묻어 있다.", key, keyHint);
            Add("key_counsel", "상담실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "양호 선생님 책상 서랍 깊숙이 들어 있던 1층 상담실 열쇠.", key, keyHint);
            Add("key_principal", "교장실 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "묵직한 놋쇠 열쇠. 손잡이에 '校長室'이라고 새겨져 있다.", key, keyHint);
            Add("key_storage", "3학년 창고 열쇠", ItemCategory.Key, ItemUse.OnlyInPlace, 0, "4층 3학년 교무실 옆 창고 열쇠. 교장실 서랍에 들어 있었다.", key, keyHint);

            // ---- 회복
            Add("medkit", "구급상자", ItemCategory.Recovery, ItemUse.Heal, 50, "소독약과 붕대가 든 구급상자. 체력을 크게 회복한다. (+50)", red);
            Add("bandage", "밴드", ItemCategory.Recovery, ItemUse.Heal, 20, "흔한 일회용 밴드 몇 장. 체력을 조금 회복한다. (+20)", new Color(0.92f, 0.78f, 0.62f));
            Add("chocobar", "초코바", ItemCategory.Recovery, ItemUse.Heal, 15, "누군가 사물함에 넣어 둔 초코바. 단 게 들어가니 조금 진정된다. (+15)", new Color(0.55f, 0.36f, 0.25f));
            Add("drink", "캔 음료", ItemCategory.Recovery, ItemUse.Drink, 10, "자판기에서 뽑은 차가운 캔 음료. 마시면 체력을 조금 회복하고 빈 캔이 남는다. (+10)", new Color(0.4f, 0.75f, 0.85f));

            // ---- 소비
            Add("empty_can", "빈 캔", ItemCategory.Consumable, ItemUse.EquipCan, 0,
                "다 마신 음료 캔. 사용하면 손에 든다. [마우스 왼쪽]으로 던지면 떨어진 곳의 소리로 선생님의 주의를 잠시 돌릴 수 있다.", new Color(0.7f, 0.72f, 0.75f));
            Add("paper", "종이", ItemCategory.Consumable, ItemUse.OnlyInPlace, 0, "줄이 그어진 빈 종이. 게시판 앞에서 펜과 함께 쓰면 지금까지의 기록을 남길 수 있다.", paper, "게시판 앞에서 펜과 함께 사용할 수 있다.");
            Add("pen", "펜", ItemCategory.Consumable, ItemUse.OnlyInPlace, 0, "잉크가 조금 남은 볼펜. 게시판 앞에서 종이와 함께 쓰면 기록을 남길 수 있다.", new Color(0.3f, 0.4f, 0.75f), "게시판 앞에서 종이와 함께 사용할 수 있다.");
            Add("battery", "건전지", ItemCategory.Consumable, ItemUse.Battery, 60, "손전등에 넣을 수 있는 건전지. 배터리를 채운다. (+60%)", new Color(0.85f, 0.75f, 0.3f));
        }
    }

    // ==================================================================== 문서
    public enum DocCategory { Note, Paper }
    public enum DocStyle { Typed, Handwritten, Diary, Book, Chat, Photo }

    public class DocDef
    {
        public string id, title, body, where;
        public DocCategory category;
        public DocStyle style;
    }

    public static class DocDB
    {
        public static readonly string[] CategoryNames = { "쪽지", "서류" };
        static Dictionary<string, DocDef> db;

        public static DocDef Get(string id)
        {
            if (db == null) Build();
            DocDef d;
            return db.TryGetValue(id, out d) ? d : null;
        }

        static void Add(string id, DocCategory cat, DocStyle style, string title, string where, string body)
        {
            db[id] = new DocDef { id = id, category = cat, style = style, title = title, where = where, body = body };
        }

        static void Build()
        {
            db = new Dictionary<string, DocDef>();
            DocCategory N = DocCategory.Note, P = DocCategory.Paper;

            // ---------------------------------------------------------------- 1층
            Add("note_shoe", N, DocStyle.Handwritten, "신발장 속 쪽지", "1층 로비 · 신발장",
@"이 쪽지를 봤다면 아직 늦지 않았다는 뜻이야.

선생님이 오면 청소함이든 화장실 칸이든
아무 데나 들어가서 문을 닫아.
문만 닫혀 있으면 그건 우리를 못 봐.

그리고 절대 뛰지 마.
그건 발소리를 듣고 와.

— 3학년 1반");

            Add("doc_program", P, DocStyle.Typed, "제1회 졸업식 거행 계획(안)", "1층 행정실",
@"青嵐高等學校 第一回 卒業式 擧行 計劃(案)

一. 日時 : 1985年 2月 ▓▓日 午前 10時   (잉크가 번져 읽을 수 없다)
二. 場所 : 本校 講堂 (5層)
三. 式順
    1. 開式辭
    2. 國民儀禮
    3. 學事報告
    4. 卒業狀 授與
    5. 校長 式辭
    6. 送辭 · 答辭
    7. 卒業歌 齊唱 (伴奏 : 3學年 1班 徐하은)
    8. 閉式辭
四. 留意事項
    가. 式 進行 中 講堂 出入門은 閉鎖함.
    나. 졸업식에 참석하지 않은 학생은 벌점 처리하며,
        담임교사가 직접 강당으로 인솔함.
    다. 卒業歌는 校長이 直接 指揮함.

                              校長 尹泰錫 (印)");

            Add("doc_expense", P, DocStyle.Typed, "지출결의서", "1층 행정실",
@"지 출 결 의 서

품    명 : 졸업 기념 명찰 장식 (교화 '철쭉' 형상, 금속)
수    량 : 298개
단    가 : 1,200원
금    액 : 357,600원

비    고 : 졸업생 287명, 교직원 11명분.
          교직원도 졸업식 당일 반드시 패용할 것.
          (교장 지시 사항)

납품 기한 : 1985년 2월 13일

결 재 :  담당 ✓   행정실장 ✓   교장 尹泰錫 (印)");

            Add("doc_certificate", P, DocStyle.Typed, "졸업증명서 발급 대장 (제1회)", "1층 행정실",
@"졸업증명서 발급 대장 — 제1회 졸업생

1985. 3. 4.   3학년 2반 박○○ (모친 방문)
              → 발급 불가. 사유 : 졸업식 미완료.

1985. 3. 11.  3학년 5반 이○○ (부친 방문)
              → 발급 불가. 사유 : 졸업식 미완료.

1985. 3. 18.  3학년 1반 서하은 (모친 방문)
              → 발급 불가. 사유 : 졸업식 미완료.
                (어머님께서 오래 우셨음.)

1985. 4. 2.   [행정실장 메모]
              제1회 졸업생 287명 전원, 학적이
              '졸업 예정'에서 바뀌지 않음.
              교장 지시로 처리 보류.
              ''식이 끝나지 않았으니 졸업도 없다.''");

            Add("book_folk", P, DocStyle.Book, "오래된 책 『향토의 제례와 금기』", "1층 도서실",
@"… 땅을 다지는 큰일에 산 사람을 바치던 풍습(人柱)은
옛 기록에도 드물지 않게 남아 있다.

많은 이를 한자리에 모아, 그들이 '끝맺지 못한 의식' 속에
머물게 하면, 그 넋은 자리를 떠나지 못하고
그 땅과 집을 영원히 떠받친다고 믿었다.

다만 의식이 끝맺어지는 순간 넋은 풀려난다.
그러므로 이 의식은 결코 끝나서는 안 된다. …

────────────────
[책 뒤표지 안쪽, 대출 카드]
대출자 : 윤태석 (교장)
  1983. 4. 2.    반납 —
  1984. 2. 14.   반납 —
  1984. 11. 30.  반납 —
  1985. 1. 9.    반납 기록 없음");

            Add("doc_counsel", P, DocStyle.Typed, "상담 일지", "1층 상담실",
@"상 담 일 지

일    시 : 1985년 2월 12일 (화) 16:30
대    상 : 3학년 1반 서하은
상담 내용 :
  졸업가 반주를 맡게 되었으나 부담을 호소함.
  교장 선생님이 따로 불러
  ''3절 마지막 소절은 치지 마라.
    노래가 끝나기 전에 식을 마칠 것이다.''
  라고 지시했다고 함.

  학생은 ''노래를 끝까지 못 부르면
  졸업한 것 같지 않을 것 같아요''라고 말함.

상담교사 소견 :
  교장 지시의 의도를 확인할 필요가 있음.
  (교장실 방문 → 면담 거절당함)");

            // ---------------------------------------------------------------- 2층 (1학년)
            Add("note_vending", N, DocStyle.Chat, "수업 중에 주고받은 쪽지", "2층 로비 · 자판기 옆",
@"야 어제 야자 째다가 학주한테 걸릴 뻔함

ㄹㅇ? 어케 튐

벌점 매긴다고 쫓아오길래 빈 캔 복도 끝으로
던졌더니 그쪽으로 가더라ㅋㅋㅋㅋ

ㅋㅋㅋ개웃기네 강아지냐

ㅇㅇ 앞으로 자판기에서 캔 하나씩 뽑아 놔
비상용임 ㅋㅋ

미친ㅋㅋ 근데 수업 중이다 쌤 본다 ㅡㅡ");

            Add("note_wonyul", N, DocStyle.Handwritten, "이원율의 과학 노트", "2층 1학년 5반",
@"[과학 필기] 산·염기 지시약  — 1학년 5반 이원율

· 리트머스 : 산성 → 붉은색 / 염기성 → 푸른색
· 페놀프탈레인 : 산성·중성 → 무색
                 염기성 → 붉은색 !!

※ 쌤이 알려 준 비밀편지 놀이
  페놀프탈레인으로 글씨 쓰고 말리면 안 보임.
  염기성 용액을 뿌리면 글씨가 붉게 나타남!
  (수산화나트륨 수용액 같은 거)

※ 과학 쌤이 시약장 비밀번호를 맨날 까먹어서
  실험 보고서 뒷면에 '비밀편지'로 써 놨다고 함ㅋㅋ

───────────
[노트 맨 뒷장, 낯선 글씨]
졸업식 노래를 끝까지 불러 줘.");

            Add("doc_duty", P, DocStyle.Typed, "졸업식 준비 업무 분장표 (1985)", "2층 1학년 교무실",
@"제1회 졸업식 준비 업무 분장

· 강당 의자 배치 ………………… 1학년부
· 졸업장·상장 준비 ……………… 3학년부
· 졸업가 반주 연습 ……………… 음악과 (반주 : 3-1 서하은)
· 명찰 장식 배부 …………………… 행정실
· 강당 비상문 점검 및 관리 …… 교장 직접
  (타인 출입 금지. 열쇠 교장 보관)

※ 졸업식 당일 양호실은 정위치 근무.
   강당 출입은 삼가 바람.");

            Add("doc_report", P, DocStyle.Handwritten, "실험 보고서", "2층 과학실",
@"실험 보고서 — 산과 염기의 중화 반응

실험 목적 : 산과 염기가 만나 중화되는 과정을 관찰한다.
결과 : (빈칸)

────────────
뒷면은 아무것도 쓰여 있지 않은 것처럼 보인다.
하지만 빛에 비춰 보면 마른 액체가 지나간 자국이 있다.");

            Add("doc_report_revealed", P, DocStyle.Handwritten, "실험 보고서 (뒷면)", "2층 과학실",
@"수산화나트륨 수용액을 뿌리자
아무것도 없던 뒷면에 붉은 글씨가 떠올랐다.

        시약장 :  4 · 1 · 7

        — 다음엔 진짜 안 까먹는다!");

            // ---------------------------------------------------------------- 3층 (2학년)
            Add("note_album", N, DocStyle.Photo, "낡은 졸업 앨범 한 장", "3층 2학년 6반 · 학급문고",
@"학급문고 책 사이에 끼워져 있던 졸업 앨범 낱장.

『청람고등학교 제1회 졸업 앨범 — 3학년 1반』

사진 속 학생들이 강당 앞 계단에 줄지어 서 있다.
맨 앞줄 가운데, 단발머리 여학생이
카메라를 보며 장난스럽게 웃고 있다.

사진 아래 이름 : 서하은

… 어디서 본 얼굴이다.
아주 가까이에서.");

            Add("note_confiscated", N, DocStyle.Chat, "압수된 쪽지", "3층 2학년 교무실 · 책상 위",
@"[압수 : 1985. 2. 14. / 3학년 1반 / 수업 중 쪽지]

하은아 아까 봤어?
교장 선생님이 강당 비상문에 쇠사슬 감고 있었어.
졸업식 날 문을 왜 잠가? 불나면 어쩌려고…

몰라 무섭게 왜 그래
그냥 내일 반주나 틀리지 마ㅋㅋ
너 3절 마지막에서 맨날 틀리잖아

안 틀려! 이번엔 끝까지 칠 거야
교장쌤이 치지 말라고 해도 칠 거임
우리 노래 끝까지 부르고 졸업하자");

            Add("doc_medical", P, DocStyle.Typed, "진료 기록부", "3층 양호실",
@"양호실 진료 기록부 — 1985. 2. ▓▓.  (물에 젖어 번져 있다)

10:41  3학년 (성명 판독 불가)
       두부 열상, 의식 없음. 맥박 약함.
       동급생 1명이 업고 내원.
       동급생 진술 : ''강당 문이 안 열려요.
       창문으로 겨우 나왔어요. 다른 애들은 아직…''
       지혈 및 기도 확보 시도.
10:58  사망.

10:59  업고 온 동급생 : 손목 골절, 얼굴 찰과상.
       부목 처치. 귀가 조치.
       → (펜으로 여러 번 그어 지움)
       → 귀가하지 않음. 다시 계단으로 올라감.");

            Add("doc_accident", P, DocStyle.Typed, "사고 경위서 (초안)", "3층 양호실",
@"사 고 경 위 서 (초안)

일    시 : 1985. 2. ▓▓. 10:30경
장    소 : 본교 강당 (5층)
경    위 : 졸업가 제창 중 노후된 천장 구조물이 붕괴함.
          비상문은 외부에서 쇠사슬로 잠겨 있었음.
          ─────────────── (두 줄로 그어 지움)
          비상문은 개방되어 있었으나, 학생들이
          당황하여 신속히 대피하지 못함.
사    망 : 학생 287명, 교직원 11명 (계 298명)

작 성 자 : 교장 尹泰錫

[여백에 다른 글씨]
거짓말. 문은 잠겨 있었다. 내가 봤다.");

            Add("note_nurse", N, DocStyle.Handwritten, "양호 선생님의 메모", "3층 양호실",
@"쿵 하는 소리가 났다. 5층이었다.

올라가 보니 강당 문이 밖에서 쇠사슬로 묶여 있었다.
안에서 아이들이 문을 두드리고 있었다.
노래가 아직 들렸다. 피아노 반주도.

교장이 문 앞에 서서 그걸 듣고 있었다.
웃고 있었다.

신고해야 한다. 교무실 전화는 끊겨 있다.
교장이 나를 봤다.

계단을 내려오는 발소리가 들린다.
이걸 누가 본다면, 제발 —");

            // ---------------------------------------------------------------- 4층 (3학년)
            Add("diary_haeun", N, DocStyle.Diary, "하은의 일기장", "4층 3학년 1반",
@"1985년 2월 14일 목요일. 맑음.

내일은 졸업식 날이다.

3년이 이렇게 빨리 지나갈 줄 몰랐다.
아침마다 늦어서 뛰어 올라오던 계단도,
매점 앞에서 줄 서던 것도 이제 끝이다.

반주 연습은 백 번은 한 것 같다.
교장 선생님은 3절 마지막 소절은 치지 말라고 하셨지만
나는 끝까지 칠 거다.
다 같이 끝까지 부르고 싶으니까.

내일이 오면 정말 끝이다.
시원한데, 좀 섭섭하다.

졸업 축하해, 나.
졸업 축하해, 우리 반.");

            Add("doc_broadcast", P, DocStyle.Typed, "방송 장비 점검표", "4층 방송실",
@"방송 장비 점검표 — 1985. 2. ▓▓.

· 강당 스피커 ………… 정상
· 교내 전체 방송 ……… 정상
· 졸업식 안내 멘트 …… 녹음 완료 (교장 육성)
   ① ''졸업식이 시작되었습니다.''
   ② ''참석하지 않은 학생에게 벌점을 주기 위해
       지금 선생님이 강당에서 출발합니다.''

────────────
전원 스위치는 내려가 있다.
그런데 녹음기의 테이프가 천천히 돌고 있다.");

            Add("doc_security", P, DocStyle.Typed, "비상문 보안 강화 요청서", "4층 3학년 교무실",
@"비상문 보안 강화 요청서

요 청 일 : 1985. 2. 9.
요 청 자 : 행정실
내    용 : 졸업식 당일 외부인 출입 통제를 위하여
          강당 비상문 2개소에 외부 시건장치(쇠사슬)를
          설치하고자 함.

결    재 : 승인 (1985. 2. 11.)
          교장 尹泰錫 (印)

[결재란 아래 교장의 친필]
열쇠는 내가 보관한다.
당일에는 누구도 열지 말 것.
식이 끝날 때까지.");

            // ---------------------------------------------------------------- 1층 교장실
            Add("note_principal", N, DocStyle.Handwritten, "교장의 메모", "1층 교장실",
@"이 학교는 오래 서 있어야 한다.
나의 학교는 결코 무너지지 않는다.

기둥에는 넋이 필요하다. 많을수록 좋다.
3년을 길러, 가장 아름다운 날에 한자리에 모은다.
졸업식 날. 모두가 강당에 있는 날.

의식은 끝나서는 안 된다.
노래가 끝나면 넋이 풀린다.
마지막 소절이 불리기 전에 천장을 —

반주하는 아이에게 일러 두었다.
마지막 소절은 치지 말라고.");

            Add("doc_principal_diary", P, DocStyle.Handwritten, "교장의 일지 (마지막 장)", "1층 교장실",
@"2월 15일.

끝나지 않았다.

그 아이가 마지막 소절을 쳤다. 시키지도 않은 것을.
문 너머로 노래가 끝까지 들렸다.
아니다. 끝나기 직전에 무너졌다. 그러니 아직이다.
식은 아직 끝나지 않았다.

양호교사가 보았다. 처리했다.

다시 강당으로 올라가 마무리해야 한다.
이번에 못 한다면 다음 졸업식에.
그 다음 졸업식에. 몇 번이고.

선생님이 데리러 갈 것이다.
참석하지 않은 아이들을.");

            Add("doc_ledger", P, DocStyle.Typed, "제1회 졸업생 명부", "1층 교장실",
@"第一回 卒業生 名簿 — 287名
敎職員 — 11名
                                    計 298

(이름이 빽빽하게 적힌 장부. 모든 이름 옆에
 붉은 점이 하나씩 찍혀 있다.)

  …
  3-1  서하은  ●●   ← 이름에 붉은 줄이 두 번 그어져 있다.
                       옆에 작은 글씨 : ''반주자. 마지막 소절.''
  …

[마지막 장]
양호교사 ●
윤태석   ○  ← 비어 있는 동그라미");

            // ---------------------------------------------------------------- 5층
            Add("note_photos", N, DocStyle.Photo, "벽에 붙은 사진들", "5층 복도",
@"복도 벽과 창문이 오래된 증명사진으로 빽빽하다.
수백 장. 모두 같은 교복, 같은 해의 얼굴들.

사진마다 뒷면에 같은 글씨가 적혀 있다.

『졸업 예정』

몇몇 사진은 얼굴 부분이 손톱으로 긁혀 있다.");
        }
    }

    // ==================================================================== 졸업가 (이 게임만의 노래)
    public static class SongData
    {
        // 악보 조각마다 들어 있는 가사 (화면에 보여 줌)
        public static readonly string[] PieceLyrics =
        {
            "1절\n철쭉꽃 붉게 피던 언덕 위 학교\n함께 웃던 날들을 가슴에 담고",
            "2절\n이제는 서로 다른 길 떠나지만\n잊지 말자 이곳에서 부른 이 노래",
            "3절\n안녕 나의 교실 안녕 나의 친구\n우리는 오늘 끝까지 졸업합니다",
        };

        // 리듬게임 화면에 흐르는 가사 (한 줄 = 2마디)
        public static readonly string[] Lines =
        {
            "철쭉꽃 붉게 피던",
            "언덕 위 학교",
            "함께 웃던 날들을",
            "가슴에 담고",
            "이제는 서로 다른",
            "길 떠나지만",
            "잊지 말자 이곳에서",
            "부른 이 노래",
            "안녕 나의 교실",
            "안녕 나의 친구",
            "우리는 오늘 끝까지",
            "졸업합니다",
        };

        public const string FinalWords = "졸업 축하드립니다.";
    }
}
