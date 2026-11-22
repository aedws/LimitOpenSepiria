# 아티팩트·콤보 아이콘(16x16). 글자 한 칸이 한 픽셀이고, 윤곽은 자동으로 둘러집니다.
from PIL import Image
import sys
O=(18,20,40,255)
PAL={'W':(255,255,255,255),'c':(203,227,254,255),'b':(151,199,254,255),'B':(100,153,232,255),'n':(59,81,130,255),'N':(45,58,103,255),
     'g':(226,190,104,255),'G':(168,128,66,255),'r':(232,72,88,255),'R':(168,36,60,255),'p':(255,150,160,255),'d':(104,20,44,255),
     's':(189,190,207,255),'S':(126,135,167,255),'k':(75,75,100,255),'y':(250,240,200,255),'Y':(214,196,150,255)}
ART={
'FrostBlade':[
"..............",
"...........W..",
"..........Wc..",
".........Wcb..",
"........WcB...",
".......WcB....",
"......WcB.....",
".....WcB......",
"....WcB.......",
".g.WcB........",
"..gcB.........",
"..Ngg.........",
".N..g.........",
"N.............",
],
'Frostmourne2':[
"......W.......",
".....WcW......",
".....WcB......",
".....WbB......",
".....WcB......",
".....WbB......",
".....WcB......",
".....WbB......",
"..S..WcB..S...",
"..SsSsbSsSS...",
"...S.kNk.S....",
".....kNk......",
".....kNk......",
"......b.......",
],
'ScoreNorthWind':[
"..............",
".yyyyyyyyyy...",
".yYYYYYYYYy...",
".ybbbbbbbby...",
".yYYYYNYYYy...",
".ybbbbNNbby...",
".yYYYYNYNYy...",
".ybbbbNbbby...",
".yYYNNNYYYy...",
".ybNNNNbbby...",
".yYYNNYYYcW...",
".yyyyyyyycWc..",
".........Wc...",
"..............",
],
'BloodstonePendant':[
"....gg..gg....",
"...g......g...",
"..g........g..",
"..g........g..",
"...g......g...",
"....g....g....",
".....gGGg.....",
".....GrrG.....",
"....GrprRG....",
"....GrrrRG....",
"....GrrRRG....",
".....GRRG.....",
"......GG......",
"..............",
],
'BloodstoneCross':[
"......gg......",
".....gssg.....",
".....gsSg.....",
".....gsSg.....",
"..gggGrrGggg..",
".gsssrprRSSSg.",
".gsSSrrRRSSSg.",
"..gggGRRGggg..",
".....gsSg.....",
".....gsSg.....",
".....gsSg.....",
".....gsSg.....",
".....gSSg.....",
"......gg......",
],
'Vitality':[
"..............",
"..rr....rr....",
".rpprr.rrrR...",
".rprrrrrrrR...",
".rrrrrrrrrR...",
".rrrrrrrrRR...",
"..rrrrrrRR....",
"...rrrrRR.....",
"....rrRR......",
".....RR.......",
"......d.......",
"..............",
"..............",
"..............",
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
sheet=Image.new('RGBA',(len(ART)*120,130),(70,90,90,255))
for i,(name,rows) in enumerate(ART.items()):
    im=render(rows); im.save(out+name+'.png')
    b=im.resize((im.width*7,im.height*7),Image.NEAREST); sheet.paste(b,(i*120+4,8),b)
sheet.save(prev)
