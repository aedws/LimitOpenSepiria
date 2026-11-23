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

# 참월 '흑'의 파열: 검은 바늘(가장자리에 옅은 보랏빛 테)과 그 주변에 흩날리는 별가루.
def needle_black():
    fw,fh,n=96,16,6; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(9)
    half=(4,3,2,1,0,0); alpha=(255,255,220,170,130,70)
    K=(14,10,24); rim=(150,120,230)
    dust=[(rnd.randrange(4,92),cy+rnd.choice((-1,1))*rnd.randrange(2,8),rnd.random()) for _ in range(26)]
    for f in range(n):
        for x in range(fw):
            t=x/(fw-1); k=1-abs(2*t-1)**1.6
            h=int(round(half[f]*k))
            if f>=4 and x%(8 if f==4 else 12)>=(5 if f==4 else 3): continue
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=K+(alpha[f],)
            if h>=1 and f<4:
                px[f*fw+x,cy-h-1]=rim+(alpha[f]*2//3,); px[f*fw+x,cy+h+1]=rim+(alpha[f]*2//3,)
        for (x,y,seed) in dust:                         # 별가루: 프레임마다 조금씩 퍼지고 깜박이다 사라집니다
            yy=y+(1 if y>cy else -1)*(f//2)
            if not (0<=yy<fh) or (int(seed*7)+f)%3==0 or f>4+seed: continue
            c=(255,255,255) if seed>0.55 else (196,170,255)
            px[f*fw+x,yy]=c+((255,255,230,190,130,70)[f],)
            if seed>0.86 and f<4:                       # 큰 별은 십자로
                for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    if 0<=x+dx<fw and 0<=yy+dy<fh: px[f*fw+x+dx,yy+dy]=c+(150,)
    sheet.save(out+'needle_black.png')

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

# 섬멸·명멸의 창: 빛으로 된 창 한 자루. 앞(오른쪽)에 능선이 있는 잎 모양 창날, 그 뒤로 곧은 자루,
# 꼬리에는 길이가 다른 결 몇 가닥이 프레임마다 일렁입니다. 짙은 색 윤곽을 둘러 창 모양이 또렷하게 보입니다.
def spear(name,pale,light,accent,dark):
    fw,fh,n=80,16,3; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    Wh=(255,255,255,255)
    head=[0,0,1,1,1,2,2,2,3,3,3,3,3,2,2,1]            # 창끝에서 뒤로 가며 반 폭(픽셀)
    for f in range(n):
        rnd=random.Random(21+f); body={}
        tip=fw-2
        for k,h in enumerate(head):                    # 창날
            x=tip-k
            for dy in range(-h,h+1):
                body[(x,cy+dy)]=Wh if dy==0 else (pale if dy<0 else light)
        base=tip-len(head)
        for dy in (-4,4): body[(base+1,cy+dy)]=light   # 미늘
        for dy in range(-2,3): body[(base,cy+dy)]=accent; body[(base-1,cy+dy)]=pale if abs(dy)<2 else accent   # 창날 밑동의 고리
        for x in range(16,base-1):                     # 자루
            body[(x,cy)]=Wh if (x+f)%7 else pale
            body[(x,cy-1)]=pale; body[(x,cy+1)]=light
        for x in range(16,base-1,9): body[(x,cy-1)]=accent; body[(x,cy+1)]=accent   # 자루 마디
        # 윤곽
        for (x,y) in list(body):
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                q=(x+dx,y+dy)
                if q not in body and 0<=q[0]<fw and 0<=q[1]<fh: px[f*fw+q[0],q[1]]=dark
        for (x,y),c in body.items(): px[f*fw+x,y]=c
        # 꼬리의 결(윤곽 없이 옅게)
        for k in range(5):
            dy=(-5,-3,0,3,5)[k]; L=rnd.randrange(8,20) if dy else rnd.randrange(10,16); x1=16 if dy==0 else rnd.randrange(18,34)
            color=(accent,light,pale,light,accent)[k]
            for x in range(max(0,x1-L),x1):
                if px[f*fw+x,cy+dy][3]==0: px[f*fw+x,cy+dy]=color[:3]+(210,)
    sheet.save(out+name+'.png')

# 창이 지나간 자리: 가는 줄기 몇 가닥과 십자 반짝임이 옅어지며 사라집니다(흰색으로 그리고 게임에서 색을 입힙니다).
def spear_trail():
    fw,fh,n=80,16,4; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(5)
    lines=[(rnd.randrange(0,44),rnd.randrange(16,36),cy+rnd.randrange(-5,6)) for _ in range(7)]
    stars=[(rnd.randrange(6,74),cy+rnd.randrange(-6,7)) for _ in range(6)]
    for f in range(n):
        a=(230,170,110,55)[f]
        for (x0,L,y) in lines[:7-f*2]:
            for x in range(x0+f*3,min(fw,x0+L-f*2)): px[f*fw+x,y]=(255,255,255,a)
        for (x,y) in stars[f:]:
            for dx,dy in ((0,0),(1,0),(-1,0),(0,1),(0,-1)):
                if 0<=x+dx<fw and 0<=y+dy<fh: px[f*fw+x+dx,y+dy]=(255,255,255,min(255,a+25))
    sheet.save(out+'spear_trail.png')

cut('cut_black',(14,10,24,255),(255,255,255,255),(236,236,246,255))
cut('cut_white',(255,255,255,255),(151,199,254,255),(190,214,255,255))
needle()
needle_black()
ring()
spear('spear_blue',(222,240,255,255),(176,218,255,255),(120,180,250,255),(26,44,104,255))
spear('spear_red',(255,240,190,255),(255,178,90,255),(232,96,52,255),(96,24,28,255))
spear_trail()
