"""Headless browser checks against the real local API and PostgreSQL."""
import json
import os
import subprocess
import time
import urllib.request
from pathlib import Path
from playwright.sync_api import sync_playwright,expect
from test_roi_api import call,s,root

demo=json.loads((root/'artifacts/demo-source.json').read_text())
camera=demo['camera']; path='/api/cameras/'+camera['id']+'/rois'
token=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))['token']
original=call('GET',path,token=token)
env=os.environ.copy();env.update(ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS='http://127.0.0.1:5279',Api__BaseUrl=s['base'])
log=(root/'artifacts/web-test.log').open('w')
web=subprocess.Popen(['dotnet',str(root/'src/Pakar.Web/bin/Debug/net10.0/Pakar.Web.dll')],cwd=root/'src/Pakar.Web',env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
evidence={}
try:
    for _ in range(60):
        try:
            urllib.request.urlopen('http://127.0.0.1:5279',timeout=1);break
        except Exception:time.sleep(.25)
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,channel='msedge')
        page=browser.new_page(viewport=dict(width=1200,height=1000))
        page.goto('http://127.0.0.1:5279/admin/polygons')
        expect(page.get_by_text('with an admin account to continue.',exact=False)).to_be_visible()
        evidence['guestBlocked']=True
        def login(page,email,password):
            page.goto('http://127.0.0.1:5279/login')
            # Existing login page prerenders; wait for the interactive circuit before input.
            page.wait_for_timeout(1000)
            page.locator('#email').fill(email);page.locator('#password').fill(password)
            page.get_by_role('button',name='Log in',exact=True).click()
            try:page.wait_for_url('**/park',timeout=15000)
            except Exception:
                page.screenshot(path=str(root/'artifacts/login-failure.png'),full_page=True)
                print('Login UI:',page.locator('body').inner_text())
                raise
        login(page,s['adminEmail'],s['adminPassword'])
        page.goto('http://127.0.0.1:5279/admin/polygons')
        try:page.get_by_label('Video source',exact=True).select_option(camera['id'],timeout=15000)
        except Exception:
            print('Editor UI:',page.locator('body').inner_text())
            page.screenshot(path=str(root/'artifacts/editor-failure.png'),full_page=True)
            raise
        expect(page.get_by_label('Parking spot',exact=True)).to_be_visible()
        page.locator('input[type=file]').set_input_files(str(root/'artifacts/parity/0000.png'))
        expect(page.get_by_text('Reference image saved.',exact=True)).to_be_visible()
        rect=page.locator('svg.polygon-surface > rect')
        expect(rect).to_be_visible()
        page.get_by_role('button',name='Clear selected polygon',exact=True).click()
        for index,(x,y) in enumerate(((.1,.1),(.25,.1),(.25,.25),(.1,.25))):
            box=rect.bounding_box()
            rect.click(position=dict(x=box['width']*x,y=box['height']*y))
            expect(page.get_by_text(f'{index+1} vertices.',exact=False)).to_be_visible()
        page.get_by_role('button',name='Save polygons',exact=True).click()
        expect(page.get_by_text('Polygons saved.',exact=False)).to_be_visible()
        saved=call('GET',path,token=token)
        first=original['spots'][0]['id']
        points=next(slot['points'] for slot in saved['slots'] if slot['spotId']==first)
        assert all(abs(a-b)<.003 for got,want in zip(points,((.1,.1),(.25,.1),(.25,.25),(.1,.25))) for a,b in zip(got,want)),points
        evidence['drawSaveThroughApi']=True
        page.reload();page.get_by_label('Video source',exact=True).select_option(camera['id'])
        expect(rect).to_be_visible()
        actual=page.locator('svg.polygon-surface > polygon').last.get_attribute('points')
        page.set_viewport_size(dict(width=650,height=1000))
        page.get_by_label('Display width',exact=True).select_option('320')
        assert page.locator('svg.polygon-surface > polygon').last.get_attribute('points')==actual
        evidence['reloadAndResizePreservePolygon']=True
        page.get_by_label('Move vertices',exact=True).check()
        box=rect.bounding_box()
        page.mouse.move(box['x']+box['width']*.1,box['y']+box['height']*.1)
        page.mouse.down();page.mouse.move(box['x']+box['width']*.15,box['y']+box['height']*.15,steps=4);page.mouse.up()
        expect(page.get_by_role('button',name='Save polygons',exact=True)).to_be_enabled()
        page.get_by_role('button',name='Save polygons',exact=True).click()
        expect(page.get_by_text('Polygons saved.',exact=False)).to_be_visible()
        points=next(slot['points'] for slot in call('GET',path,token=token)['slots'] if slot['spotId']==first)
        assert abs(points[0][0]-.15)<.01 and abs(points[0][1]-.15)<.01,points
        evidence['dragAtDifferentScale']=True
        page.screenshot(path=str(root/'artifacts/polygon-editor.png'),full_page=True)
        page.get_by_label('Move vertices',exact=True).uncheck()
        page.get_by_role('button',name='Clear selected polygon',exact=True).click()
        for x,y in ((.1,.1),(.2,.2)):
            box=rect.bounding_box();rect.click(position=dict(x=box['width']*x,y=box['height']*y))
        page.get_by_role('button',name='Save polygons',exact=True).click()
        expect(page.get_by_role('alert')).to_contain_text('3 to 50')
        evidence['invalidPolygonRejected']=True
        page.get_by_role('button',name='Clear selected polygon',exact=True).click()
        page.get_by_role('button',name='Save polygons',exact=True).click()
        expect(page.get_by_text('Polygons saved.',exact=False)).to_be_visible()
        assert first not in [slot['spotId'] for slot in call('GET',path,token=token)['slots']]
        evidence['clearPersists']=True
        user=browser.new_page()
        login(user,'roi-user@example.test','Local-roi-test-password-123')
        user.goto('http://127.0.0.1:5279/admin/polygons')
        expect(user.get_by_text('Access denied',exact=True)).to_be_visible()
        expect(user.get_by_label('Video source',exact=True)).to_have_count(0)
        evidence['nonAdminBlocked']=True
        browser.close()
    evidence['passed']=True
finally:
    call('PUT',path,dict(slots=[dict(spotId=slot['spotId'],points=slot['points']) for slot in original['slots']]),token=token)
    web.terminate();web.wait(timeout=15);log.close()
(root/'docs/ai/editor-evidence.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence,indent=2))
