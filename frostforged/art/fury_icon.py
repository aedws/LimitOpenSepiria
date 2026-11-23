# 블루비틀 퓨리 상태 아이콘(18x18): 본판 상태 아이콘과 같은 짜임(2픽셀 검은 테, 색 바탕, 흰 무늬).
from PIL import Image
import sys
K=(0,0,0,255); B=(62,112,204,255); W=(255,255,255,255); C=(151,199,254,255)
im=Image.new('RGBA',(18,18),(0,0,0,0)); px=im.load()
for y in range(18):
    for x in range(18):
        if (x in (0,17) and y in (0,17)): continue
        edge=min(x,y,17-x,17-y)
        px[x,y]=K if edge<2 or (x in (2,15) and y in (2,15)) else B
for i in range(10): px[4+i,13-i]=W                 # 창 자루
for p in ((11,4),(12,4),(13,5),(13,6),(12,5),(11,5),(12,6),(10,5),(12,7)): px[p]=W   # 창끝
for p in ((4,9),(5,8),(6,7),(8,13),(9,12),(10,11)): px[p]=C    # 돌진선
im.save(sys.argv[1]); im.resize((180,180),Image.NEAREST).save(sys.argv[2])

# 화염기수의 화염 날개 깃 상태 아이콘
B2=(200,72,48,255); Y=(255,228,135,255)
im=Image.new('RGBA',(18,18),(0,0,0,0)); px=im.load()
for y in range(18):
    for x in range(18):
        if (x in (0,17) and y in (0,17)): continue
        edge=min(x,y,17-x,17-y)
        px[x,y]=K if edge<2 or (x in (2,15) and y in (2,15)) else B2
for i in range(9): px[5+i,13-i]=W                                  # 깃대
for p in ((9,5),(10,5),(11,4),(8,6),(9,6),(7,7),(8,7),(6,8),(7,8),(6,9),(5,10),(6,10),(5,11)): px[p]=Y   # 위쪽 깃가지
for p in ((12,7),(13,6),(11,8),(12,8),(10,9),(11,9),(9,10),(10,10),(8,11),(9,11),(7,12)): px[p]=W   # 아래쪽 깃가지
import os
im.save(os.path.join(os.path.dirname(sys.argv[1]),'feather.png'))

# 태극 발동형 아티팩트의 충전 상태 아이콘 4종: 참월 흑(가로선), 참월 백(세로선), 섬멸 청, 명멸 적
def hud(bg):
    im=Image.new('RGBA',(18,18),(0,0,0,0)); px=im.load()
    for y in range(18):
        for x in range(18):
            if (x in (0,17) and y in (0,17)): continue
            edge=min(x,y,17-x,17-y)
            px[x,y]=K if edge<2 or (x in (2,15) and y in (2,15)) else bg
    return im,px
base=os.path.dirname(sys.argv[1])
im,px=hud((70,52,110,255))                                  # 참월 흑: 보라 바탕에 검은 심·흰 테의 가로선
for x in range(3,15): px[x,8]=W; px[x,9]=(14,10,24,255); px[x,10]=W
im.save(os.path.join(base,'taegeuk_0.png'))
im,px=hud((40,44,70,255))                                   # 참월 백: 어두운 바탕에 흰 심·푸른 테의 세로선
for y in range(3,15): px[8,y]=C; px[9,y]=W; px[10,y]=C
im.save(os.path.join(base,'taegeuk_1.png'))
for n,bg,body,tip in ((2,(40,84,160,255),(176,218,255,255),W),(3,(150,50,40,255),(255,190,100,255),W)):
    im,px=hud(bg)                                           # 창: 대각선 빛줄기
    for i in range(9): px[4+i,13-i]=body
    for i in range(6): px[7+i,12-i]=body
    for p in ((12,4),(13,4),(13,5),(12,5),(11,5),(12,6)): px[p]=tip
    im.save(os.path.join(base,'taegeuk_%d.png'%n))
