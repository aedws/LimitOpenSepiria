# 우주 속성 이펙트 중 코드로 그리는 것들: 참월(cut_black, cut_white, needle, ring), 섬멸·명멸의 창(spear_blue, spear_red).
# 프레임을 가로로 이어 붙인 한 줄로 저장합니다. (void.png, ignorance.png 는 이미지 생성 API 로 만든 뒤 fx_slice.py 로 잘랐습니다.)
from PIL import Image
import random, sys
out=sys.argv[1]
W=(255,255,255,255); G=(214,220,236,255)

# 참월: 화면을 가르는 면도날 같은 직선. 처음엔 또렷한 선(가운데 심 + 양쪽 테), 이어서 가는 선, 마지막엔 점선으로 끊기며 사라집니다.
def cut(name,core,rim,fade):
    fw,fh,n=96,8,12; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    for f in range(n):
        for x in range(fw):
            X=f*fw+x
            if f<5:                                   # 또렷한 선: 테-심-테
                px[X,cy-1]=rim; px[X,cy]=core; px[X,cy+1]=rim
                if f==0: px[X,cy-2]=rim[:3]+(110,); px[X,cy+2]=rim[:3]+(110,)   # 긋는 순간 한 겹 더 번쩍
            elif f<8:                                 # 가는 선
                px[X,cy]=fade[:3]+((200,150,110)[f-5],)
            else:                                     # 점선(갈수록 듬성듬성)
                period=(6,8,10,12)[f-8]; on=(4,4,3,2)[f-8]
                if x%period<on: px[X,cy]=fade[:3]+((120,95,70,45)[f-8],)
    sheet.save(out+name+'.png')

# 파열: 양 끝이 뾰족한 흰 바늘(렌즈 모양)이 번쩍였다가 희미해지며 점선으로 흩어집니다.
def needle():
    fw,fh,n=96,12,6; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    half=(4,3,2,1,0,0); alpha=(255,255,200,140,110,60)
    for f in range(n):
        for x in range(fw):
            t=x/(fw-1); k=1-abs(2*t-1)**1.6
            h=int(round(half[f]*k))
            if f>=4 and x%(8 if f==4 else 12)>=(5 if f==4 else 3): continue
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=(255,255,255,alpha[f])
    sheet.save(out+'needle.png')

# 충격 고리: 가는 흰 원이 퍼져 나갑니다.
def ring():
    import math
    fw,n=48,5; sheet=Image.new('RGBA',(fw*n,fw),(0,0,0,0)); px=sheet.load(); c=(fw-1)/2
    for f in range(n):
        r=5+f*4.5; a=(255,230,180,120,60)[f]
        for y in range(fw):
            for x in range(fw):
                d=math.hypot(x-c,y-c)
                if abs(d-r)<0.6: px[f*fw+x,y]=(255,255,255,a)
    sheet.save(out+'ring.png')

# 섬멸·명멸의 창: 볼루스파처럼 화면을 꿰뚫는 빛줄기. 굵고 흰 심이 앞(오른쪽)으로 뾰족하게 빠지고,
# 양옆에 길이가 제각각인 결(끝이 계단처럼 끊긴 줄기)이 나란히 붙어 속도감을 냅니다. 프레임마다 결이 조금씩 흔들립니다.
def spear(name,pale,light,accent):
    fw,fh,n=160,32,3; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    Wh=(255,255,255,255)
    for f in range(n):
        rnd=random.Random(11+f)
        def bar(x0,x1,y,color,th=1):
            for x in range(max(0,x0),min(fw,x1)):
                for t in range(th):
                    if 0<=y+t<fh: px[f*fw+x,y+t]=color
        # 결: 가운데에서 멀수록 짧고 옅게. 끝은 2~4픽셀씩 계단으로 끊깁니다.
        for side in (-1,1):
            for k in range(1,8):
                dy=side*(k*2-(f%2)*(k%2))
                length=int((118-k*13)*rnd.uniform(0.8,1.1)); start=int(rnd.uniform(4,34))+k*5
                color=pale if k<3 else light if k<6 else accent
                th=2 if k<3 else 1
                y=cy+dy-(th-1 if side<0 else 0)
                bar(start,start+length,y,color,th)
                step=rnd.randrange(2,5)                      # 계단 끝
                bar(start+length,start+length+step*2,y+(0 if side<0 else th-1),color,1)
                bar(start-step*2,start,y+(th-1 if side<0 else 0),color,1)
                if rnd.random()<0.5: bar(start-step*5,start-step*3,y,accent,1)   # 떨어져 나간 조각
        # 심: 뒤는 뭉툭, 앞은 뾰족
        for x in range(10,fw-2):
            t=(x-10)/(fw-12)
            half=3.6*(1-max(0.0,(t-0.62)/0.38)**1.4)*min(1.0,t/0.08+0.35)
            h=int(round(half))
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=Wh
            if h>=2:                                         # 심 가장자리에 옅은 테
                px[f*fw+x,cy-h-1]=pale; px[f*fw+x,cy+h+1]=pale
    sheet.save(out+name+'.png')

# 창이 지나간 자리: 가는 줄기 몇 가닥과 십자 반짝임이 옅어지며 사라집니다(흰색으로 그리고 게임에서 색을 입힙니다).
def spear_trail():
    fw,fh,n=160,32,4; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(5)
    lines=[(rnd.randrange(0,90),rnd.randrange(30,70),cy+rnd.randrange(-9,10)) for _ in range(9)]
    stars=[(rnd.randrange(10,150),cy+rnd.randrange(-11,12)) for _ in range(7)]
    for f in range(n):
        a=(230,170,110,55)[f]
        for (x0,L,y) in lines[:9-f*2]:
            for x in range(x0+f*6,min(fw,x0+L-f*4)): px[f*fw+x,y]=(255,255,255,a)
        for (x,y) in stars[f:]:
            for dx,dy in ((0,0),(1,0),(-1,0),(0,1),(0,-1)):
                if 0<=x+dx<fw and 0<=y+dy<fh: px[f*fw+x+dx,y+dy]=(255,255,255,min(255,a+25))
    sheet.save(out+'spear_trail.png')

cut('cut_black',(14,10,24,255),(255,255,255,255),(236,236,246,255))
cut('cut_white',(255,255,255,255),(151,199,254,255),(190,214,255,255))
needle()
ring()
spear('spear_blue',(222,240,255,255),(176,218,255,255),(120,180,250,255))
spear('spear_red',(255,240,190,255),(255,178,90,255),(232,96,52,255))
spear_trail()
