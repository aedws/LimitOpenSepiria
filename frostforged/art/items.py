# 아티팩트·콤보 아이콘(본판 아이콘과 같은 20px 안팎). 글자 한 칸이 한 픽셀이고, 윤곽은 자동으로 둘러집니다.
from PIL import Image
import sys
O=(30,28,46,255)
PAL={'W':(255,255,255,255),'c':(203,227,254,255),'b':(151,199,254,255),'B':(100,153,232,255),'n':(59,81,130,255),'N':(45,58,103,255),
     'g':(226,190,104,255),'G':(168,128,66,255),'r':(232,72,88,255),'R':(168,36,60,255),'p':(255,150,160,255),'d':(104,20,44,255),
     's':(189,190,207,255),'S':(126,135,167,255),'k':(75,75,100,255),'w':(232,243,255,255),'a':(120,232,255,255),'y':(250,240,200,255),'Y':(214,196,150,255)}
ART={
'FrostBlade':[
".................WW.",
"...a............WWcb",
"..aWa..........WWcbB",
"...a..........WWcbB.",
".............WwcbB..",
"............WwcbB...",
"...........WwcbB....",
"..........WwcbB.....",
".........WwcbB......",
"........WwcbB.......",
".......WwcbB........",
"..g...WwcbB.........",
"..gG.WwcbB......a...",
"...gGgcbB......aWa..",
"....gGgB........a...",
"...NngGg............",
"..NnN.gGg...........",
".NnN...gg...........",
"gGN.................",
"gg..................",
],
'Frostmourne2':[
"........W........",
".......WcB.......",
"......WwcbB......",
"......WcabB......",
"......WwcbB......",
"......WcabB......",
"......WwcbB......",
"......WcabB......",
"......WwcbB......",
"......WcabB......",
"......WwcbB......",
".S....WcabB....S.",
".sS...WwcbB...SS.",
".ssSSsSSaSSsSSSk.",
"..SkksSaWaSkkkk..",
"....k.SSaSk.k....",
".......kNk.......",
".......nNk.......",
".......kNk.......",
"......SsaSk......",
".......SSk.......",
],
'ScoreNorthWind':[
"..yyyyyyyyyyyyyy..",
".yYyyyyyyyyyyyyYy.",
".YyyyyyyyyyyyyyyY.",
"..ySSSSSSSSSSSSy..",
"..yyyyyyyyNyyyyy..",
"..ySSSSSSSNNSSSy..",
"..yyyyyyyyNyNyyy..",
"..ySSSSSSSNSSNSy..",
"..yyyyyyyyNyyyyy..",
"..ySSSSNNNNSSSSy..",
"..yyyyNNNNNyyyyy..",
"..ySSSNNNNNSSSSy..",
"..yyyyyNNNyyyyyy..",
"..yyaaaayyyyaayy..",
"..yayyyyaayaayyy..",
"..yyyyyyyyayyyyy..",
".YyyyyyyyyyyyyyyY.",
".yYyyyyyyyyyyyyYy.",
"..YYYYYYYYYYYYYY..",
],
'BloodstonePendant':[
"....gggggggg....",
"...gG......Gg...",
"..gG........Gg..",
"..g..........g..",
"..g..........g..",
"..gG........Gg..",
"...gG......Gg...",
"....gG....Gg....",
".....gGggGg.....",
"......GggG......",
".....GgrrgG.....",
"....GrpprrRG....",
"...GrpWprrRRG...",
"...GrpprrrRRG...",
"...GrrrrrRRdG...",
"...GrrrrRRRdG...",
"....GrrRRRdG....",
".....GRRRdG.....",
"......GRdG......",
".......GG.......",
],
'BloodstoneCross':[
"......ggggg......",
"......gsssG......",
"......gsSSG......",
"......gsSSG......",
"......gsSSG......",
"ggggggGgrgGgggggg",
"gsssssGrprRGSSSSG",
"gsSSSGrpWrRRGSSkG",
"gsSSSSGrrRRGSSSkG",
"gSSSSSSGRRGSSkkkG",
"GGGGGGgsSSGGGGGGG",
"......gsSSG......",
"......gsSSG......",
"......gsSSG......",
"......gsSSG......",
"......gsSSG......",
"......gsSkG......",
"......gSkkG......",
"......GGGGG......",
],
'Vitality':[
"...rrrr....rrrr...",
"..rpprrr..rrrrrR..",
".rpWprrrrrrrrrrRR.",
"rrpprrrrrrrrrrrRRR",
"rrrrrrrrrrrrrrrRRR",
"rrrrrrrrrrrrrrRRRR",
"rrrrrrrrrrrrrrRRRd",
".rrrrrrrrrrrrRRRd.",
"..rrrrrrrrrrRRRd..",
"...rrrrrrrrRRRd...",
"....rrrrrrRRRd....",
".....rrrrRRRd.....",
"......rrRRRd......",
".......RRRd.......",
"........Rd........",
],
}
def render(rows):
    h=len(rows); w=max(len(r) for r in rows)
    body={}
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch!='.': body[(x+1,y+1)]=PAL[ch]
    im=Image.new('RGBA',(w+2,h+2),(0,0,0,0)); px=im.load()
    for (x,y),c in body.items(): px[x,y]=c
    for (x,y) in body:
        for dx,dy in((1,0),(-1,0),(0,1),(0,-1)):
            n=(x+dx,y+dy)
            if 0<=n[0]<w+2 and 0<=n[1]<h+2 and n not in body: px[n[0],n[1]]=O
    return im
out,prev=sys.argv[1],sys.argv[2]
sheet=Image.new('RGBA',(len(ART)*170,180),(70,90,90,255))
for i,(name,rows) in enumerate(ART.items()):
    im=render(rows); im.save(out+name+'.png')
    b=im.resize((im.width*7,im.height*7),Image.NEAREST); sheet.paste(b,(i*170+4,8),b)
sheet.save(prev)
