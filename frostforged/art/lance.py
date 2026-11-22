# 서리기수 : 돌격창 블루비틀의 손에 든 그림(창끝이 위). 글자 한 칸이 한 픽셀이고 윤곽이 자동으로 둘러집니다.
from PIL import Image
import sys
O=(27,24,40,255)
PAL={'W':(255,255,255,255),'c':(203,227,254,255),'b':(120,178,240,255),'B':(62,112,204,255),'n':(36,58,140,255),
     'a':(120,232,255,255),'s':(189,190,207,255),'S':(126,135,167,255),'k':(80,84,110,255)}
HEAD=[
"...W...",
"...W...",
"...W...",
"...W...",
"...Wb..",
"...Wb..",
"..cWb..",
"..cWb..",
"..cWb..",
"..cWb..",
"..cWB..",
"..cWB..",
".ccWbB.",
".ccWbB.",
".ccWbB.",
".ccWbB.",
".caWbB.",
".caWBB.",
"cc.a.BB",
"c..a..B",
"...s...",
"..sSk..",
"...S...",
]
SHAFT=["...B...","...n...","...n...","...b...","...n...","...n..."]
BUTT=[
"..sSk..",
"...b...",
"...c...",
"...W...",
]
rows=HEAD+[SHAFT[i%len(SHAFT)] for i in range(15)]+BUTT
# 다른 봉류 무기처럼 윤곽은 2픽셀(5x5 에서 네 귀퉁이를 뺀 모양). 가장자리가 잘리지 않게 바깥에 빈 줄을 한 줄 둡니다.
RING=[(dx,dy) for dx in range(-2,3) for dy in range(-2,3) if abs(dx)+abs(dy)<4]
def render(rows):
    h=len(rows); w=len(rows[0]); m=3; body={}
    for y,row in enumerate(rows):
        for x,ch in enumerate(row):
            if ch!='.': body[(x+m,y+m)]=PAL[ch]
    im=Image.new('RGBA',(w+2*m,h+2*m),(0,0,0,0)); px=im.load()
    for (x,y) in body:
        for dx,dy in RING: px[x+dx,y+dy]=O
    for (x,y),c in body.items(): px[x,y]=c
    return im
out,prev=sys.argv[1],sys.argv[2]
im=render(rows); im.save(out+'lance.png'); print(im.size)
b=im.resize((im.width*10,im.height*10),Image.NEAREST)
sheet=Image.new('RGBA',(b.width*2+60,b.height+20),(70,90,90,255)); sheet.paste(b,(10,10),b)
r=im.rotate(-25,expand=True,resample=Image.NEAREST).resize((im.width*6,im.height*6),Image.NEAREST) if False else b.rotate(-30,expand=True,resample=Image.NEAREST)
sheet.paste(r.resize((r.width//2,r.height//2),Image.NEAREST),(b.width+30,10),r.resize((r.width//2,r.height//2),Image.NEAREST))
sheet.save(prev)
