#!/usr/bin/env python3

import os

from flask import abort, flash, Flask, Markup, redirect, render_template, request, url_for
from flask_login import current_user, login_required, login_user, logout_user, LoginManager
from sqlalchemy.sql.expression import exists

from forms import LoginForm, RegisterForm, SignForm, VerifyForm, DocumentForm
from notary import Notary
from models import db, Document, User


DB_URI = 'sqlite:///data.db'
DOCS_PER_PAGE = 200
login_manager = LoginManager()


def create_app():
    secret_key_file = 'secret.key'

    if not os.path.exists(secret_key_file):
        with open(secret_key_file, 'wb') as file:
            file.write(os.urandom(16))
    
    with open(secret_key_file, 'rb') as file:
        secret_key = file.read().hex()

    app = Flask(__name__)
    app.config['SQLALCHEMY_DATABASE_URI'] = DB_URI
    app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False
    app.config['SECRET_KEY'] = secret_key
    login_manager.init_app(app)
    db.init_app(app)

    return app


app = create_app()


@login_manager.user_loader
def load_user(user_id):
    return User.query.filter(User.id == str(user_id)).first()


@app.route('/signup', methods=['GET', 'POST'])
def signup():
    if current_user.is_authenticated:
        return redirect(url_for('user', user_id=current_user.id))

    form = RegisterForm()
    
    if request.method == 'POST' and form.validate_on_submit():
        if db.session.query(exists().where(User.username == request.form['username'])).scalar():
            form.username.errors.append('This username is taken')
        else:
            user = User(
                username=request.form['username'],
                name=request.form['name'],
                phone=request.form['phone'],
                address=request.form['address'])
    
            db.session.add(user)
            db.session.commit()
            login_user(user)
            flash(Markup(
                f'Your autogenerated password is <strong>{user.generate_password()}</strong>.'
                f' Store it somewhere safe!'))
    
            return redirect(url_for('user', user_id=user.id))
    
    return render_template('signup.html', form=form)


@app.route('/login', methods=['GET', 'POST'])
def login():
    if current_user.is_authenticated:
        return redirect(url_for('user', user_id=current_user.id))

    form = LoginForm()

    if request.method == 'POST' and form.validate_on_submit():
        user = User.query.filter_by(username=request.form['username']).first()
    
        if user is not None and user.verify_password(request.form['password']):
            login_user(user)
            flash('You were logged in.')
            
            return redirect(url_for('user', user_id=user.id))
        else:
            form.password.errors.append('Incorrect username or password')
    
    return render_template('login.html', form=form)


@app.route('/logout')
def logout():
    if current_user.is_authenticated:
        logout_user()
        flash('You were logged out.')

    return redirect('/')


@app.route('/user/<int:user_id>')
def user(user_id):
    user = User.query.get(user_id)
    
    if user is None:
        return abort(404)

    is_current_user = current_user.is_authenticated and current_user.id == user.id
    
    return render_template('user.html', user=user, is_current_user=is_current_user)


@app.route('/doc/<int:doc_id>', methods=['GET', 'POST'])
def doc(doc_id):
    doc = Document.query.get(doc_id)
    
    if doc is None:
        return abort(404)
    
    visible = doc.is_public or current_user.is_authenticated and current_user.id == doc.author_id
    
    form = DocumentForm()
    
    if request.method == 'POST' and form.validate_on_submit():
        password = request.form['password']
    
        if not doc.verify_password(password):
            form.password.errors.append('Incorrect password')
        else:
            visible = True
        
    return render_template('doc.html', form=form, doc=doc, visible=visible)


@app.route('/')
def recent_docs():
    page = request.args.get('page', 1, type=int)
    docs = Document.query.order_by(Document.id.desc()).paginate(page=page, per_page=DOCS_PER_PAGE)
    
    current_user_id = current_user.id if current_user.is_authenticated else None
    
    return render_template('docs.html', docs=docs, current_user_id=current_user_id)


@app.route('/sign', methods=['GET', 'POST'])
def sign():
    form = SignForm()

    if request.method == 'GET' or not form.validate_on_submit():
        return render_template('sign.html', form=form)

    document = Document(
        author=current_user,
        title=request.form['title'],
        text=request.form['text'],
        is_public=request.form.get('is_public') == 'on')

    db.session.add(document)
    db.session.commit()

    if not document.is_public:
        flash(Markup(
            f'Document\'s autogenerated password is <strong>{document.generate_password()}</strong>.'
            f' Store it somewhere safe!'))

    return redirect(url_for('doc', doc_id=document.id))


@app.route('/verify', methods=['GET', 'POST'])
def verify():
    form = VerifyForm()

    error = None
    
    if request.method == 'POST' and form.validate_on_submit():
        if Notary.verify(form.public_key.data, form.title.data, form.text.data, form.signature.data):
            flash('The signature is valid')
        else:
            error = 'The signature is invalid'
    
    return render_template('verify.html', form=form, error=error)


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=17171)
