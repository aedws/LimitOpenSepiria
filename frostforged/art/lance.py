# 서리기수 : 돌격창 블루비틀의 손에 든 그림(창끝이 위). 글자 한 칸이 한 픽셀이고 윤곽이 자동으로 둘러집니다.
# lance_icon.png(아이콘)은 같은 그림에 2픽셀 윤곽을 두른 것으로, 따로 보관합니다.
from PIL import Image
import sys
O=(27,24,40,255)
PAL={'W':(255,255,255,255),'e':(203,227,254,255),'b':(151,199,254,255),'c':(100,153,232,255),'d':(59,81,130,255),
     'a':(120,232,255,255),'s':(189,190,207,255),'S':(126,135,167,255),'k':(85,89,116,255),'x':O}
# 본판의 찌르는 창들과 같은 짜임: 가운데 능선이 있는 긴 창날, 아래로 갈라지는 날개, 1픽셀 자루, 물미.
HEAD=[
"....W....",
"....W....",
"....W....",
"....W....",
"...WWb...",
"...WWb...",
"...Wec...",
"...Wec...",
"..WWecb..",
"..WeWcb..",
"..WeWcb..",
"..WcxWb..",
".WWcxcbb.",
".Wb.x.cb.",
"WW..a..bb",
"W...a...b",
"....d....",
"...sSk...",
"....d....",
]
SHAFT=["....c....","....b....","....b....","....c....","....b....","....d...."]
BUTT=[
"...sSk...",
"....e....",
"....W....",
"....W....",
]
rows=HEAD+[SHAFT[i%len(SHAFT)] for i in range(14)]+BUTT
# 손에 든 그림의 윤곽은 본판 봉류와 같은 1픽셀입니다(위·아래·좌·우).
RING=[(1,0),(-1,0),(0,1),(0,-1)]
def render(rows):
    h=len(rows); w=len(rows[0]); m=1; body={}
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch!='.': body[(x+m,y+m)]=PAL[ch]
    im=Image.new('RGBA',(w+2*m,h+2*m),(0,0,0,0)); px=im.load()
    for (x,y) in body:
        for dx,dy in RING: px[x+dx,y+dy]=O
    for (x,y),c in body.items(): px[x,y]=c
    return im
# 기수별 배색(같은 모양, 색만 다름). 손에 든 그림은 1픽셀 윤곽, 아이콘은 2픽셀 윤곽입니다.
BLUE=dict(PAL)
VARIANTS={
 'knight': {'W':(255,255,255),'e':(222,224,234),'b':(189,190,207),'c':(126,135,167),'d':(80,84,110),'a':(237,177,46)},
 'flame':  {'W':(255,255,255),'e':(255,228,135),'b':(255,160,96),'c':(223,113,38),'d':(130,45,46),'a':(251,242,54)},
 'thunder':{'W':(255,255,255),'e':(255,247,170),'b':(251,226,80),'c':(160,130,235),'d':(75,60,140),'a':(190,255,255)},
 'doom':   {'W':(235,225,245),'e':(200,160,235),'b':(150,95,200),'c':(95,55,140),'d':(50,35,80),'a':(206,89,81)},
}
RING2=[(dx,dy) for dx in range(-2,3) for dy in range(-2,3) if abs(dx)+abs(dy)<4]
def draw(pal,ring,m):
    h=len(rows); w=len(rows[0]); body={}
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch!='.': body[(x+m,y+m)]=pal[ch]
    im=Image.new('RGBA',(w+2*m,h+2*m),(0,0,0,0)); px=im.load()
    for (x,y) in body:
        for dx,dy in ring: px[x+dx,y+dy]=O
    for (x,y),c in body.items(): px[x,y]=c
    return im
out,prev=sys.argv[1],sys.argv[2]
sheet=Image.new('RGBA',(5*110,260),(70,90,90,255))
im=render(rows); im.save(out+'lance.png'); b=im.resize((im.width*5,im.height*5),Image.NEAREST); sheet.paste(b,(10,10),b)
for i,(name,colors) in enumerate(VARIANTS.items()):
    pal=dict(BLUE); pal.update({k:v+(255,) for k,v in colors.items()})
    draw(pal,RING,1).save(out+'lance_%s.png'%name)
    icon=draw(pal,RING2,3); icon.save(out+'lance_%s_icon.png'%name)
    b=icon.resize((icon.width*5,icon.height*5),Image.NEAREST); sheet.paste(b,(110*(i+1)+10,10),b)
sheet.save(prev)
