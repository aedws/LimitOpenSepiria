# 아티팩트·콤보 아이콘. 글자 한 칸이 한 픽셀이고, 본판 아이콘처럼 2픽셀 두께의 어두운 윤곽이 자동으로 둘러집니다.
# 색도 본판 아이콘에서 쓰는 계열(강철·금·핏빛)에 얼음색만 더했습니다.
from PIL import Image
import sys
O=(34,32,52,255)
PAL={'W':(255,255,255,255),
     # 얼음
     'c':(203,227,254,255),'b':(151,199,254,255),'B':(100,153,232,255),'n':(59,81,130,255),'a':(120,232,255,255),
     # 강철
     's':(189,190,207,255),'S':(126,135,167,255),'k':(80,84,110,255),'K':(57,58,81,255),
     # 금
     'y':(251,242,54,255),'g':(237,177,46,255),'o':(223,113,38,255),'u':(181,68,33,255),
     # 핏빛
     'p':(230,172,168,255),'r':(206,89,81,255),'R':(183,50,39,255),'d':(134,44,43,255),'D':(98,36,40,255),
     # 양피지
     'e':(244,232,196,255),'E':(214,190,140,255),
     # 악보의 먹색
     't':(77,97,115,255),'T':(106,141,148,255)}
ART={
'FrostBlade':[
"............Wc.",
"...........Wcb.",
"..........Wab..",
".........Wcb...",
"........Wcb....",
".......Wab.....",
"......Wcb......",
"..y..Wcb.......",
"...gWab........",
"....g..........",
"...k.g.........",
"..K...o........",
".k.............",
"g..............",
],
'Frostmourne2':[
".....a.....",
"....kSk....",
".....k.....",
".....S.....",
".....k.....",
"S.........S",
"sS..kak..Ss",
".sSSkWkSSk.",
"..k.cWb.k..",
"....cWb....",
"....caB....",
"....cWb....",
"....caB....",
"....cWb....",
"....caB....",
"....cWb....",
"....caB....",
"....cWb....",
".....cb....",
".....c.....",
],
'ScoreNorthWind':[
"......bbbbb.....W.",
"....bBaaaaaBb..WaW",
"...bB.WWWWWWaBb.W.",
"..bB.WccccccWaBb..",
".bB..WcttttcW.aB..",
".bB..WccccccW..Bb.",
".bB..WctntccW..b..",
".bBa.WccnnccW.....",
"..bBaaWcnnttW..W..",
"...bBBaaaccccWaW..",
"....bbBBaaaWWaab..",
".....WcbBBBaaabB..",
"..a..WccccbBBBb...",
".aWa..WWWWW.bb....",
"..a...............",
],
'BloodstonePendant':[
".....ggg.....",
".....g.g.....",
".....ggg.....",
"......o......",
"......g......",
"......r......",
"......p......",
".....rWR.....",
"....rpWRd....",
".rrppWWWRRdd.",
"....rRWRd....",
".....RRd.....",
"......R......",
"......d......",
"......D......",
],
'BloodstoneCross':[
"...ygo...",
"...gRo...",
"...gro...",
"...ygo...",
"yggyWgggo",
"grrWpWRRo",
"ogggWgoou",
"...ygo...",
"...gro...",
"...gRo...",
"...gRo...",
"...gdo...",
"...gdo...",
"...ogu...",
"....u....",
],
# 블루비틀 전용: 일번창 : 에린다누아
'Erindanua':[
".............WW.",
"...........ccW..",
".........cccWb..",
"........ccbWbb..",
"........cbWbbB..",
"........bWbBB...",
".......gWBB.....",
"......kgg.......",
".....Kk.........",
"....kK..........",
"...Kk...........",
"..kK............",
".gk.............",
"yg..............",
],
'Vitality':[
".prr...rrR.",
"prWrr.rrRRd",
"prrrrrrRRRd",
"rrrrrrRRRRd",
".rrrrRRRRd.",
"..rrRRRRd..",
"...RRRRd...",
"....RRd....",
".....d.....",
],
}
# 블루비틀 전용: 서리 바람의 날개깃 (둥글게 말린 깃, 흰 깃대가 아래에서 말려 올라감)
ART['FrostWingFeather']=[
".....ccccWW.",
"...ccccWWcc.",
"..cbbWWccc..",
".cbbWWbbc...",
".bbWWbbb....",
"bbaWbbb.....",
"baaWaab.....",
"BaWWaaB.....",
"BBWaaBB..ss.",
"BBWBBBB...s.",
".BBWBBn...s.",
".nBBWnn..s..",
"..nnnWsss...",
"....nn......",
]
ART={k:ART[k] for k in ('FrostBlade','Frostmourne2','ScoreNorthWind','BloodstonePendant','BloodstoneCross','FrostWingFeather','Erindanua','Vitality')}
# ── 우주 속성 아이콘 ──────────────────────────────────────────────────────
PAL.update({'v':(168,133,255,255),'V':(95,55,140,255),'m':(214,196,255,255),'q':(40,32,58,255)})
def swap(rows,table):
    return [''.join(table.get(ch,ch) for ch in row) for row in rows]
RING8=["..vvvv..",".vV..Vv.","vV....Vv","v......v","v......v","vV....Vv",".vVmmVv.","..vWWv.."]
ARC=[".......xx",".....xxXx","...xxXXx.","..xXXx...",".xXXx....","xXXx.....","xXx......","xx......."]
YINYANG=["..WWWW..",".WWWWqq.","WWqWWqqq","WWWWqqqq","WWWWqqqq","WWWqqWqq",".WWqqqq.","..qqqq.."]
ART.update({
 'PurpleRing':RING8,
 'CrescentRing':swap(RING8,{'v':'s','V':'S','m':'c'}),
 'BlackBracelet':["..qqqq..",".qkvvkq.","qk....kq","q......q","q......q","qk....kq",".qkvvkq.","..qqqq.."],
 'IgnoranceBug':["..q..q..","...qq...",".qVVVVq.","qVvVVvVq","qVVmmVVq","qVvVVvVq",".qVVVVq.","..q..q.."],
 'YinCrescent':["...WWc..",".WWc....","WWc.....","Wc......","Wc......","WWc.....",".WWc....","...WWc.."],
 'YangSun':["....y....",".y..y..y.","..y.g.y..","...ggg...","yygyWygyy","...ggg...","..y.g.y..",".y..y..y.","....y...."],
 'SunMoonGale':[".EeeeeeE.","EeeeeeeeE",".eyyevve.",".eygevVe.",".eyyevve.",".eeeeeee.","EeeeeeeeE",".EeeeeeE."],
 'ScoreSolarEclipse':swap(ART['ScoreNorthWind'],{'a':'y','b':'g','B':'o'}),
 'ScoreLunarEclipse':swap(ART['ScoreNorthWind'],{'a':'m','b':'v','B':'V'}),
 'ProphecyDoom':[".qqqqqqq.","qVVVVVVVq","qVVmVmVVq","qVVVWVVVq","qVVmVmVVq","qVVVVVVVq","qeeeeeeeq",".qqqqqqq."],
 'ChamwolBlack':swap(ARC,{'x':'V','X':'q'}),
 'ChamwolWhite':swap(ARC,{'x':'s','X':'W'}),
 'AnnihilationBlue':swap(ART['Erindanua'],{'g':'s','y':'s'}),
 'FlickerRed':swap(ART['Erindanua'],{'c':'y','b':'g','B':'o','g':'R','y':'r'}),
 'Dharani':[".gg.....gg.","gEEeeeeeEEg","gEeetteeeEg","gEeettteeEg","gEeeetteeEg","gEEeeeeeEEg",".gg.....gg."],
 'TaegeukGate':YINYANG,
 'Eclipse':["..yggy..",".gqqqqg.","yqqqqqqy","gqqqqqqg","gqqqqqqg","yqqqqqqy",".gqqqqg.","..yggy.."],
 'Taegeuk':swap(YINYANG,{'W':'m','q':'V'}),
})
# 2픽셀 윤곽: 5x5 에서 네 귀퉁이를 뺀 모양
RING=[(dx,dy) for dx in range(-2,3) for dy in range(-2,3) if abs(dx)+abs(dy)<4]
def render(rows):
    h=len(rows); w=max(len(r) for r in rows); m=2
    body={}
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch!='.': body[(x+m,y+m)]=PAL[ch]
    im=Image.new('RGBA',(w+2*m,h+2*m),(0,0,0,0)); px=im.load()
    for (x,y) in body:
        for dx,dy in RING: px[x+dx,y+dy]=O
    for (x,y),c in body.items(): px[x,y]=c
    return im
out,prev=sys.argv[1],sys.argv[2]
COLS=9
sheet=Image.new('RGBA',(COLS*130,((len(ART)+COLS-1)//COLS)*150),(70,90,90,255))
for i,(name,rows) in enumerate(ART.items()):
    im=render(rows); im.save(out+name+'.png')
    b=im.resize((im.width*5,im.height*5),Image.NEAREST); sheet.paste(b,((i%COLS)*130+6,(i//COLS)*150+8),b)
sheet.save(prev)
