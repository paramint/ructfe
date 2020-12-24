import argparse

def get_parsed_args():
    parser = argparse.ArgumentParser(description="client for mudaraba service")
    parser.add_argument('-r', '--register', dest='register',
                        action='store_true',
                        help="register user")
    parser.add_argument('-gu', '--get-user', dest='get_user',
                        action='store_true',
                        help="get user info")
    parser.add_argument('-gc', '--get-cookie', dest='get_cookie',
                        action='store_true',
                        help="get user's cookie")
    parser.add_argument('-gt', '--get-transactions', dest='get_transactions',
                        action='store_true',
                        help="get all transactions of user specified by login")
    parser.add_argument('-gp', '--get-pubkey', dest='get_pubkey',
                        action='store_true',
                        help="get pubkey of user specified by login")
    parser.add_argument('-s', '--send-money', dest='send_money',
                        action='store_true',
                        help="send money")
    parser.add_argument('-l', '--login',
                        help='username',
                        default=None, dest='login')
    parser.add_argument('-p', '--password',
                        help='user\'s password',
                        default=None, dest='password')
    parser.add_argument('-c', '--cookie',
                        help='user\'s cookie',
                        default=None, dest='cookie')
    parser.add_argument('-cr', '--credit-card-credentials',
                        help='user\'s credit card creds',
                        default="--", dest='credit_card_credentials')
    parser.add_argument('-t', '--login-to',
                        help='username who will receive the money',
                        default=None, dest='login_to')
    parser.add_argument('-d', '--description',
                        help='description for transaction',
                        default="--", dest='description')
    parser.add_argument('-a', '--amount',
                        help='description for transaction',
                        default=0, dest='amount')
    parser.add_argument('-k', '--priv-key',
                        help='private key for crypt transaction description',
                        default=None, dest='priv_key')
    return parser.parse_args()